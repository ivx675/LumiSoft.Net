using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using LumiSoft.Net.DNS;
using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// Provides inbound SMTP sender validation via reverse DNS (PTR), SPF record evaluation,
    /// and MX/Domain IP fallback authorization without exception-driven control flow.
    /// </summary>
    public static class SMTP_SenderValidator
    {
        #region record Result

        /// <summary>
        /// Represents the outcome of an inbound SMTP sender validation check,
        /// returning an <see cref="SMTP_ServerResponse"/> without throwing exceptions for policy failures.
        /// </summary>
        /// <param name="IsValid"><see langword="true"/> if the sender passed validation; otherwise <see langword="false"/>.</param>
        /// <param name="MatchedRule">A diagnostic identifier for the rule or check that passed, or <see langword="null"/> if rejected.</param>
        /// <param name="Response">The <see cref="SMTP_ServerResponse"/> to send to the SMTP client.</param>
        public sealed record Result(
            bool IsValid,
            string? MatchedRule,
            SMTP_ServerResponse Response)
        {
            /// <summary>
            /// Creates a passing <see cref="Result"/> with SMTP <c>250 2.1.0</c>.
            /// </summary>
            /// <param name="matchedRule">The validation check or mechanism that succeeded.</param>
            /// <returns>A passing <see cref="Result"/>.</returns>
            public static Result Pass(string matchedRule) => new(
                true,
                matchedRule,
                new SMTP_ServerResponse(
                    250,
                    new SMTP_t_EnhancedStatusCode(2, 1, 0),
                    "Sender validation PASSED successfully."));

            /// <summary>
            /// Creates a temporary rejection <see cref="Result"/> with SMTP <c>451</c> and RFC 3463 enhanced status code.
            /// </summary>
            /// <param name="errorText">The diagnostic reason for the temporary failure.</param>
            /// <param name="subject">The RFC 3463 subject code (defaults to <c>4</c> for network/routing).</param>
            /// <param name="detail">The RFC 3463 detail code (defaults to <c>43</c> for <c>4.4.3</c>).</param>
            /// <returns>A temporary failure <see cref="Result"/>.</returns>
            public static Result TemporaryError(string errorText, int subject = 4, int detail = 43) => new(
                false,
                null,
                new SMTP_ServerResponse(
                    451,
                    new SMTP_t_EnhancedStatusCode(4, subject, detail),
                    $"Temporary Error (Try Again Later): {errorText}"));

            /// <summary>
            /// Creates a permanent rejection <see cref="Result"/> with SMTP <c>550</c> and RFC 3463 enhanced status code.
            /// </summary>
            /// <param name="errorText">The diagnostic reason for the permanent rejection.</param>
            /// <param name="subject">The RFC 3463 subject code (defaults to <c>7</c> for security/policy).</param>
            /// <param name="detail">The RFC 3463 detail code (defaults to <c>1</c> for <c>5.7.1</c>).</param>
            /// <returns>A permanent failure <see cref="Result"/>.</returns>
            public static Result PermanentReject(string errorText, int subject = 7, int detail = 1) => new(
                false,
                null,
                new SMTP_ServerResponse(
                    550,
                    new SMTP_t_EnhancedStatusCode(5, subject, detail),
                    $"Permanent Rejection (Policy Violation): {errorText}"));
        }

        #endregion

        #region enum StepStatus

        /// <summary>
        /// Internal classification of an individual validation step's result.
        /// </summary>
        private enum StepStatus
        {
            Pass,
            NotApplicable,
            TemporaryFailure,
            PermanentFailure
        }

        #endregion

        #region recod StepOutcome

        /// <summary>
        /// Internal result wrapper produced by individual PTR, SPF, and MX/Domain fallback checks.
        /// </summary>
        private sealed record StepOutcome(
            StepStatus Status,
            string? MatchedRule,
            Result? FailureResult)
        {
            public static StepOutcome Passed(string matchedRule) => new(StepStatus.Pass, matchedRule, null);
            public static StepOutcome None() => new(StepStatus.NotApplicable, null, null);
            public static StepOutcome TempFail(Result result) => new(StepStatus.TemporaryFailure, null, result);
            public static StepOutcome PermFail(Result result) => new(StepStatus.PermanentFailure, null, result);
        }

        #endregion

        private const int MaxSpfLookups = 10;

        #region method ValidateAsync

        /// <summary>
        /// Validates an incoming SMTP sender against domain resolution, SPF policy evaluation,
        /// MX/domain IP fallback (when no SPF record is published), and PTR verification.
        /// </summary>
        /// <param name="remoteIP">The remote client IP address initiating the SMTP connection.</param>
        /// <param name="ehloName">The hostname or domain provided in the client's <c>EHLO</c>/<c>HELO</c> command.</param>
        /// <param name="mailFrom">The reverse-path (<c>MAIL FROM</c>) sender address or mailbox.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> resolving to a <see cref="Result"/> indicating whether validation
        /// passed, temporarily failed, or was permanently rejected.
        /// </returns>
        public static async Task<Result> ValidateAsync(
            IPAddress? remoteIP,
            string? ehloName,
            string? mailFrom)
        {
            if (remoteIP == null) {
                return Result.PermanentReject(
                    "Remote client IP address is missing.",
                    subject: 7,
                    detail: 1);
            }

            // Normalize IPv4-mapped IPv6 addresses (e.g. ::ffff:192.0.2.1) to standard IPv4 for consistent matching.
            IPAddress normalizedIp = remoteIP.IsIPv4MappedToIPv6 ? remoteIP.MapToIPv4() : remoteIP;

            string normalizedMailbox = NormalizeMailbox(mailFrom);
            string? targetDomain = ExtractValidationDomain(normalizedMailbox, ehloName);
            if (string.IsNullOrWhiteSpace(targetDomain)) {
                return Result.PermanentReject(
                    "Unable to determine a valid sender or EHLO domain for validation.",
                    subject: 7,
                    detail: 1);
            }

            // Evaluate SPF first so legitimate senders with valid SPF (and split/pool PTR records) pass cleanly.
            var visitedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            StepOutcome spfOutcome = await EvaluateSpfRecursiveAsync(targetDomain, normalizedIp, visitedDomains, lookupCount: 0)
                .ConfigureAwait(false);

            if (spfOutcome.Status == StepStatus.Pass && spfOutcome.MatchedRule != null) {
                return Result.Pass(spfOutcome.MatchedRule);
            }
            if (spfOutcome.Status == StepStatus.TemporaryFailure && spfOutcome.FailureResult != null) {
                return spfOutcome.FailureResult;
            }
            if (spfOutcome.Status == StepStatus.PermanentFailure && spfOutcome.FailureResult != null) {
                return spfOutcome.FailureResult;
            }

            // No SPF record published for targetDomain: fall back to host A/AAAA and MX target IPs.
            StepOutcome fallbackOutcome = await VerifyMxOrDomainFallbackAsync(targetDomain, normalizedIp)
                .ConfigureAwait(false);

            if (fallbackOutcome.Status == StepStatus.Pass && fallbackOutcome.MatchedRule != null) {
                return Result.Pass(fallbackOutcome.MatchedRule);
            }
            if (fallbackOutcome.Status == StepStatus.TemporaryFailure && fallbackOutcome.FailureResult != null) {
                return fallbackOutcome.FailureResult;
            }

            // Only run the PTR check after SPF and MX/Domain IP fallback do not pass.
            StepOutcome ptrOutcome = await VerifyPtrAsync(normalizedIp, ehloName).ConfigureAwait(false);
            if (ptrOutcome.Status == StepStatus.TemporaryFailure && ptrOutcome.FailureResult != null) {
                return ptrOutcome.FailureResult;
            }
            if (ptrOutcome.Status == StepStatus.PermanentFailure && ptrOutcome.FailureResult != null) {
                return ptrOutcome.FailureResult;
            }

            return fallbackOutcome.FailureResult ?? Result.PermanentReject(
                $"Sender validation failed for domain '{targetDomain}' and IP '{normalizedIp}'.",
                subject: 7,
                detail: 1);
        }

        #endregion


        #region method VerifyPtrAsync

        /// <summary>
        /// Verifies reverse DNS (PTR) for <paramref name="remoteIP"/> and checks that at least one resolved
        /// PTR hostname matches <paramref name="ehloName"/>.
        /// </summary>
        private static async Task<StepOutcome> VerifyPtrAsync(IPAddress remoteIP, string? ehloName)
        {
            string cleanEhlo = (ehloName ?? string.Empty).Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(cleanEhlo)) {
                return StepOutcome.PermFail(Result.PermanentReject(
                    "Missing EHLO/HELO hostname for PTR validation.",
                    subject: 7,
                    detail: 25));
            }

            try {
                if (remoteIP.AddressFamily != AddressFamily.InterNetwork &&
                    remoteIP.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    return StepOutcome.PermFail(Result.PermanentReject(
                        $"Unsupported IP address format '{remoteIP}' for PTR lookup.",
                        subject: 7,
                        detail: 25));
                }

                DNS_ServerResponse? ptrResponse = await DNS_Client.Static
                    .QueryAsync(remoteIP.ToString(), DNS_RecordType.PTR, 1000)
                    .ConfigureAwait(false);

                if (ptrResponse == null) {
                    return StepOutcome.TempFail(Result.TemporaryError(
                        $"No DNS response received for PTR query on IP '{remoteIP}'."));
                }

                if (ptrResponse.ResponseCode == DNS_ResponseCode.ServerFailure) {
                    return StepOutcome.TempFail(Result.TemporaryError(
                        $"Temporary DNS SERVFAIL querying PTR for '{remoteIP}'."));
                }

                if (ptrResponse.ResponseCode == DNS_ResponseCode.NameError ||
                    ptrResponse.ResponseCode != DNS_ResponseCode.NoError)
                {
                    return StepOutcome.PermFail(Result.PermanentReject(
                        $"PTR lookup failed for IP '{remoteIP}' ({ptrResponse.ResponseCode}).",
                        subject: 7,
                        detail: 25));
                }

                List<string> ptrHosts = ptrResponse.Answers.PTR
                    .Select(p => (p.DomainName ?? string.Empty).Trim().TrimEnd('.'))
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .ToList();

                if (ptrHosts.Any(h => string.Equals(h, cleanEhlo, StringComparison.OrdinalIgnoreCase))) {
                    return StepOutcome.Passed($"ptr:{cleanEhlo}");
                }

                return StepOutcome.PermFail(Result.PermanentReject(
                    $"PTR mismatch: IP '{remoteIP}' resolves to [{string.Join(", ", ptrHosts)}], expected '{cleanEhlo}'.",
                    subject: 7,
                    detail: 25));
            }
            catch (Exception ex) when (IsTransientException(ex)) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"Temporary DNS error during PTR lookup for '{remoteIP}': {ex.Message}"));
            }
            catch (Exception ex) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"Unexpected error during PTR lookup for '{remoteIP}': {ex.Message}"));
            }
        }

        #endregion

        #region method EvaluateSpfRecursiveAsync

        /// <summary>
        /// Evaluates SPF TXT records for <paramref name="domain"/> recursively, running independent
        /// <c>include:</c>, <c>a:</c>, and <c>mx:</c> checks concurrently.
        /// Returns <see cref="StepStatus.NotApplicable"/> when no SPF record exists for <paramref name="domain"/>.
        /// </summary>
        private static async Task<StepOutcome> EvaluateSpfRecursiveAsync(
            string domain,
            IPAddress remoteIP,
            HashSet<string> visitedDomains,
            int lookupCount,
            bool failOnRevisit = true)
        {
            string cleanDomain = domain.Trim().TrimEnd('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(cleanDomain)) {
                return StepOutcome.None();
            }

            if (lookupCount >= MaxSpfLookups) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"SPF lookup limit ({MaxSpfLookups}) exceeded while evaluating '{cleanDomain}'."));
            }

            if (!visitedDomains.Add(cleanDomain)) {
                return failOnRevisit
                    ? StepOutcome.TempFail(Result.TemporaryError(
                        $"SPF loop detected for domain '{cleanDomain}'."))
                    : StepOutcome.None();
            }

            try {
                DNS_ServerResponse? txtResponse = await DNS_Client.Static
                    .QueryAsync(cleanDomain, DNS_RecordType.TXT, 1000)
                    .ConfigureAwait(false);

                if (txtResponse == null) {
                    return StepOutcome.TempFail(Result.TemporaryError(
                        $"No DNS response received for SPF TXT query on '{cleanDomain}'."));
                }

                if (txtResponse.ResponseCode == DNS_ResponseCode.ServerFailure) {
                    return StepOutcome.TempFail(Result.TemporaryError(
                        $"Temporary DNS SERVFAIL querying SPF TXT for '{cleanDomain}'."));
                }

                if (txtResponse.ResponseCode == DNS_ResponseCode.NameError) {
                    return StepOutcome.None();
                }

                if (txtResponse.ResponseCode != DNS_ResponseCode.NoError) {
                    return StepOutcome.TempFail(Result.TemporaryError(
                        $"Unexpected DNS response code '{txtResponse.ResponseCode}' querying SPF for '{cleanDomain}'."));
                }

                var spfRecords = new List<string>();
                foreach (var txtAnswer in txtResponse.Answers.TXT) {
                    string txtValue = txtAnswer.Text;
                    if (txtValue.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase) &&
                        (txtValue.Length == 6 || char.IsWhiteSpace(txtValue[6])))
                    {
                        spfRecords.Add(txtValue);
                    }
                }

                if (spfRecords.Count == 0) {
                    return StepOutcome.None();
                }

                if (spfRecords.Count > 1) {
                    return StepOutcome.PermFail(Result.PermanentReject(
                        $"Multiple SPF records published for '{cleanDomain}'.",
                        subject: 7,
                        detail: 23));
                }

                string spfRecord = spfRecords[0];
                string[] parts = spfRecord.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                StepOutcome? explicitAllOutcome = null;
                StepOutcome? deferredPermFail = null;

                var includedDomains = new List<string>();
                var aTargetDomains = new List<string>();
                var mxTargetDomains = new List<string>();
                bool hasRedirect = false;
                string? redirectDomain = null;

                foreach (string rawPart in parts.Skip(1)) {
                    string part = StripQualifier(rawPart, out char qualifier);

                    if (part.StartsWith("redirect=", StringComparison.OrdinalIgnoreCase)) {
                        string candidateRedirect = part.Substring(9).Trim().TrimEnd('.');
                        if (!string.IsNullOrEmpty(candidateRedirect)) {
                            hasRedirect = true;
                            redirectDomain = candidateRedirect;
                        }
                    }
                    else if (part.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase)) {
                        string cidrSpec = part.Substring(4);
                        if (IpMatchesCidr(remoteIP, cidrSpec)) {
                            return EvaluateQualifierMatch(qualifier, $"spf:ip4:{cidrSpec}@{cleanDomain}", cleanDomain, remoteIP);
                        }
                    }
                    else if (part.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase)) {
                        string cidrSpec = part.Substring(4);
                        if (IpMatchesCidr(remoteIP, cidrSpec)) {
                            return EvaluateQualifierMatch(qualifier, $"spf:ip6:{cidrSpec}@{cleanDomain}", cleanDomain, remoteIP);
                        }
                    }
                    else if (part.StartsWith("include:", StringComparison.OrdinalIgnoreCase)) {
                        string includedDomain = part.Substring(8).Trim().TrimEnd('.');
                        if (!string.IsNullOrEmpty(includedDomain)) {
                            includedDomains.Add(includedDomain);
                        }
                    }
                    else if (part.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                             part.StartsWith("a:", StringComparison.OrdinalIgnoreCase))
                    {
                        string targetDomain = (part.StartsWith("a:", StringComparison.OrdinalIgnoreCase)
                            ? part.Substring(2)
                            : cleanDomain).Trim().TrimEnd('.');
                        if (!string.IsNullOrEmpty(targetDomain)) {
                            aTargetDomains.Add(targetDomain);
                        }
                    }
                    else if (part.Equals("mx", StringComparison.OrdinalIgnoreCase) ||
                             part.StartsWith("mx:", StringComparison.OrdinalIgnoreCase))
                    {
                        string targetDomain = (part.StartsWith("mx:", StringComparison.OrdinalIgnoreCase)
                            ? part.Substring(3)
                            : cleanDomain).Trim().TrimEnd('.');
                        if (!string.IsNullOrEmpty(targetDomain)) {
                            mxTargetDomains.Add(targetDomain);
                        }
                    }
                    else if (part.Equals("all", StringComparison.OrdinalIgnoreCase)) {
                        if (qualifier == '+') {
                            return StepOutcome.Passed($"spf:+all@{cleanDomain}");
                        }
                        if (qualifier == '-') {
                            explicitAllOutcome ??= StepOutcome.PermFail(Result.PermanentReject(
                                $"SPF policy violation: IP '{remoteIP}' is rejected by '-all' on '{cleanDomain}'.",
                                subject: 7,
                                detail: 23));
                        }
                        // ~all or ?all: do not return early before checking includes/a/mx/redirect.
                    }
                }

                int nextLookupCount = lookupCount + 1;

                if (includedDomains.Count > 0 || aTargetDomains.Count > 0 || mxTargetDomains.Count > 0) {
                    var parallelTasks = new List<Task<StepOutcome>>(
                        includedDomains.Count + aTargetDomains.Count + mxTargetDomains.Count);

                    foreach (string incDomain in includedDomains) {
                        parallelTasks.Add(EvaluateSpfRecursiveAsync(
                            incDomain,
                            remoteIP,
                            new HashSet<string>(
                                visitedDomains,
                                StringComparer.OrdinalIgnoreCase),
                            nextLookupCount,
                            failOnRevisit: false));
                    }

                    foreach (string aDomain in aTargetDomains) {
                        parallelTasks.Add(DomainHasIpAsync(aDomain, remoteIP));
                    }

                    foreach (string mxDomain in mxTargetDomains) {
                        parallelTasks.Add(MxHasIpAsync(mxDomain, remoteIP));
                    }

                    StepOutcome[] parallelOutcomes = await Task.WhenAll(parallelTasks).ConfigureAwait(false);

                    foreach (StepOutcome outcome in parallelOutcomes) {
                        if (outcome.Status == StepStatus.Pass || outcome.Status == StepStatus.TemporaryFailure) {
                            return outcome;
                        }
                        if (outcome.Status == StepStatus.PermanentFailure && outcome.FailureResult != null) {
                            deferredPermFail ??= outcome;
                        }
                    }
                }

                if (hasRedirect && !string.IsNullOrEmpty(redirectDomain)) {
                    StepOutcome redirectOutcome = await EvaluateSpfRecursiveAsync(
                        redirectDomain,
                        remoteIP,
                        new HashSet<string>(
                            visitedDomains,
                            StringComparer.OrdinalIgnoreCase),
                        nextLookupCount,
                        failOnRevisit: true).ConfigureAwait(false);

                    if (redirectOutcome.Status == StepStatus.Pass || redirectOutcome.Status == StepStatus.TemporaryFailure) {
                        return redirectOutcome;
                    }
                    if (redirectOutcome.Status == StepStatus.PermanentFailure && redirectOutcome.FailureResult != null) {
                        deferredPermFail ??= redirectOutcome;
                    }
                }

                return deferredPermFail ?? explicitAllOutcome ?? StepOutcome.PermFail(Result.PermanentReject(
                    $"SPF policy violation: IP '{remoteIP}' is not authorized by the SPF record for '{cleanDomain}'.",
                    subject: 7,
                    detail: 23));
            }
            catch (Exception ex) when (IsTransientException(ex)) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"Temporary DNS timeout/error during SPF evaluation for '{cleanDomain}': {ex.Message}"));
            }
            catch (Exception ex) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"DNS processing error for SPF records on '{cleanDomain}': {ex.Message}"));
            }
        }

        #endregion

        #region method EvaluateQualifierMatch

        private static StepOutcome EvaluateQualifierMatch(char qualifier, string ruleName, string domain, IPAddress remoteIP)
        {
            return qualifier switch
            {
                '-' => StepOutcome.PermFail(Result.PermanentReject(
                    $"SPF policy violation: IP '{remoteIP}' matched fail mechanism '{ruleName}' on '{domain}'.",
                    subject: 7,
                    detail: 23)),
                _ => StepOutcome.Passed(ruleName)
            };
        }

        #endregion

        #region method StripQualifier

        private static string StripQualifier(string token, out char qualifier)
        {
            if (token.Length > 0 && (token[0] == '+' || token[0] == '-' || token[0] == '~' || token[0] == '?')) {
                qualifier = token[0];
                return token.Substring(1);
            }

            qualifier = '+';
            return token;
        }

        #endregion

        #region method IpMatchesCidr

        private static bool IpMatchesCidr(IPAddress remoteIP, string cidrSpec)
        {
            if (string.IsNullOrWhiteSpace(cidrSpec)) {
                return false;
            }

            string trimmed = cidrSpec.Trim();
            int slashIndex = trimmed.IndexOf('/');
            string ipPart = slashIndex >= 0 ? trimmed.Substring(0, slashIndex) : trimmed;

            if (!IPAddress.TryParse(ipPart, out IPAddress? networkAddress) || networkAddress == null) {
                return false;
            }

            if (networkAddress.IsIPv4MappedToIPv6) {
                networkAddress = networkAddress.MapToIPv4();
            }

            if (remoteIP.AddressFamily != networkAddress.AddressFamily) {
                return false;
            }

            byte[] ipBytes = remoteIP.GetAddressBytes();
            byte[] netBytes = networkAddress.GetAddressBytes();
            int maxPrefix = ipBytes.Length * 8;

            int prefixLength = maxPrefix;
            if (slashIndex >= 0) {
                string prefixStr = trimmed.Substring(slashIndex + 1);
                if (!int.TryParse(prefixStr, out prefixLength) || prefixLength < 0 || prefixLength > maxPrefix) {
                    return false;
                }
            }

            int fullBytes = prefixLength / 8;
            int remBits = prefixLength % 8;

            for (int i = 0; i < fullBytes; i++) {
                if (ipBytes[i] != netBytes[i]) {
                    return false;
                }
            }

            if (remBits > 0 && fullBytes < ipBytes.Length) {
                int mask = (0xFF << (8 - remBits)) & 0xFF;
                if ((ipBytes[fullBytes] & mask) != (netBytes[fullBytes] & mask)) {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region method ExtractValidationDomain

        private static string? ExtractValidationDomain(string normalizedMailbox, string? ehloName)
        {
            if (!string.IsNullOrEmpty(normalizedMailbox)) {
                int atIndex = normalizedMailbox.LastIndexOf('@');
                if (atIndex > 0 && atIndex < normalizedMailbox.Length - 1) {
                    return normalizedMailbox.Substring(atIndex + 1).Trim().TrimEnd('.');
                }
            }

            string cleanEhlo = (ehloName ?? string.Empty).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(cleanEhlo) ? null : cleanEhlo;
        }

        #endregion

        #region method DomainHasIpAsync

        /// <summary>
        /// Resolves a domain's host A/AAAA addresses via <see cref="DNS_Client.Static"/> and checks whether any match <paramref name="remoteIP"/>.
        /// </summary>
        private static async Task<StepOutcome> DomainHasIpAsync(string domain, IPAddress remoteIP)
        {
            try {
                IPAddress[] ips = await DNS_Client.Static.GetHostAddressesAsync(domain).ConfigureAwait(false);
                if (ips != null && ips.Any(ip =>
                    {
                        IPAddress candidate = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
                        return candidate.Equals(remoteIP);
                    }))
                {
                    return StepOutcome.Passed($"spf:a:{domain}");
                }

                return StepOutcome.None();
            }
            catch (Exception ex) when (IsTransientException(ex)) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"Temporary DNS error resolving host addresses for '{domain}': {ex.Message}"));
            }
            catch {
                return StepOutcome.None();
            }
        }

        #endregion

        #region method MxHasIpAsync

        /// <summary>
        /// Queries a domain's MX records, resolves their target host addresses via <see cref="DNS_Client.Static"/>,
        /// and checks whether any match <paramref name="remoteIP"/>.
        /// </summary>
        private static async Task<StepOutcome> MxHasIpAsync(string domain, IPAddress remoteIP)
        {
            try {
                DNS_ServerResponse? mxResponse = await DNS_Client.Static
                    .QueryAsync(domain, DNS_RecordType.MX, 1000)
                    .ConfigureAwait(false);

                if (mxResponse == null || mxResponse.ResponseCode != DNS_ResponseCode.NoError) {
                    if (mxResponse != null && mxResponse.ResponseCode == DNS_ResponseCode.ServerFailure) {
                        return StepOutcome.TempFail(Result.TemporaryError(
                            $"Temporary DNS failure querying MX records for '{domain}'."));
                    }

                    return StepOutcome.None();
                }

                List<string> mxHosts = mxResponse.Answers.MX
                    .Select(mx => (mx.Host ?? string.Empty).Trim().TrimEnd('.'))
                    .Where(host => !string.IsNullOrEmpty(host))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (mxHosts.Count > 0) {
                    Task<StepOutcome>[] hostTasks = mxHosts
                        .Select(host => DomainHasIpAsync(host, remoteIP))
                        .ToArray();

                    StepOutcome[] hostOutcomes = await Task.WhenAll(hostTasks).ConfigureAwait(false);

                    foreach (StepOutcome hostOutcome in hostOutcomes) {
                        if (hostOutcome.Status == StepStatus.TemporaryFailure) {
                            return hostOutcome;
                        }
                        if (hostOutcome.Status == StepStatus.Pass) {
                            return StepOutcome.Passed($"spf:mx:{hostOutcome.MatchedRule?.Split(':').LastOrDefault() ?? domain}");
                        }
                    }
                }
            }
            catch (Exception ex) when (IsTransientException(ex)) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"Temporary DNS timeout/error querying MX records for '{domain}': {ex.Message}"));
            }
            catch {
                // Non-transient DNS failure -> no matching MX IP.
            }

            return StepOutcome.None();
        }

        #endregion

        #region method VerifyMxOrDomainFallbackAsync

        /// <summary>
        /// Validates fallback authorization when no SPF record is published, checking both the domain's own
        /// host IP addresses and its MX record host IP addresses via <see cref="DNS_Client.Static"/> against <paramref name="remoteIP"/>.
        /// </summary>
        private static async Task<StepOutcome> VerifyMxOrDomainFallbackAsync(string senderDomain, IPAddress remoteIP)
        {
            var authorizedIPs = new HashSet<IPAddress>();
            bool dnsQuerySucceeded = false;

            Task<(IPAddress[]? Ips, StepOutcome? TempError)> domainTask = ResolveHostAddressesSafeAsync(senderDomain);
            Task<(DNS_ServerResponse? Response, StepOutcome? TempError)> mxQueryTask = QueryMxRecordsSafeAsync(senderDomain);

            await Task.WhenAll(domainTask, mxQueryTask).ConfigureAwait(false);

            var domainResult = await domainTask.ConfigureAwait(false);
            if (domainResult.TempError != null) {
                return domainResult.TempError;
            }
            if (domainResult.Ips != null && domainResult.Ips.Length > 0) {
                foreach (IPAddress ip in domainResult.Ips) {
                    authorizedIPs.Add(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
                }
                dnsQuerySucceeded = true;
            }

            var mxResult = await mxQueryTask.ConfigureAwait(false);
            if (mxResult.TempError != null) {
                return mxResult.TempError;
            }
            if (mxResult.Response != null && mxResult.Response.ResponseCode == DNS_ResponseCode.NoError) {
                List<string> mxHosts = mxResult.Response.Answers.MX
                    .Select(mx => (mx.Host ?? string.Empty).Trim().TrimEnd('.'))
                    .Where(host => !string.IsNullOrEmpty(host))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (mxHosts.Count > 0) {
                    Task<(IPAddress[]? Ips, StepOutcome? TempError)>[] mxHostTasks = mxHosts
                        .Select(host => ResolveHostAddressesSafeAsync(host))
                        .ToArray();

                    (IPAddress[]? Ips, StepOutcome? TempError)[] mxHostResults =
                        await Task.WhenAll(mxHostTasks).ConfigureAwait(false);

                    foreach (var hostRes in mxHostResults) {
                        if (hostRes.TempError != null) {
                            return hostRes.TempError;
                        }
                        if (hostRes.Ips != null) {
                            foreach (IPAddress ip in hostRes.Ips) {
                                authorizedIPs.Add(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
                            }
                        }
                    }
                }

                dnsQuerySucceeded = true;
            }

            if (authorizedIPs.Any(ip => ip.Equals(remoteIP))) {
                return StepOutcome.Passed($"fallback:mx-or-a:{senderDomain}");
            }

            if (!dnsQuerySucceeded) {
                return StepOutcome.TempFail(Result.TemporaryError(
                    $"Temporary DNS failure: Unable to resolve host or MX records for '{senderDomain}'."));
            }

            return StepOutcome.PermFail(Result.PermanentReject(
                $"Sender validation failed: No valid SPF record found, and remote IP '{remoteIP}' is not listed in domain host or MX records for '{senderDomain}'.",
                subject: 7,
                detail: 1));
        }

        #endregion

        #region method ResolveHostAddressesSafeAsync

        private static async Task<(IPAddress[]? Ips, StepOutcome? TempError)> ResolveHostAddressesSafeAsync(string targetDomain)
        {
            try {
                IPAddress[] ips = await DNS_Client.Static.GetHostAddressesAsync(targetDomain).ConfigureAwait(false);
                return (ips, null);
            }
            catch (Exception ex) when (IsTransientException(ex)) {
                return (null, StepOutcome.TempFail(Result.TemporaryError(
                    $"Temporary DNS error resolving host addresses for '{targetDomain}': {ex.Message}")));
            }
            catch {
                return (null, null);
            }
        }

        #endregion

        #region method QueryMxRecordsSafeAsync

        private static async Task<(DNS_ServerResponse? Response, StepOutcome? TempError)> QueryMxRecordsSafeAsync(string targetDomain)
        {
            try {
                DNS_ServerResponse? mxResponse = await DNS_Client.Static
                    .QueryAsync(targetDomain, DNS_RecordType.MX, 1000)
                    .ConfigureAwait(false);

                if (mxResponse != null && mxResponse.ResponseCode == DNS_ResponseCode.ServerFailure) {
                    return (null, StepOutcome.TempFail(Result.TemporaryError(
                        $"Temporary DNS failure querying MX records for '{targetDomain}'.")));
                }

                return (mxResponse, null);
            }
            catch (Exception ex) when (IsTransientException(ex)) {
                return (null, StepOutcome.TempFail(Result.TemporaryError(
                    $"Temporary DNS timeout/error querying MX records for '{targetDomain}': {ex.Message}")));
            }
            catch {
                return (null, null);
            }
        }

        #endregion

        #region method NormalizeMailbox

        private static string NormalizeMailbox(string? mailbox)
        {
            if (string.IsNullOrWhiteSpace(mailbox)) {
                return string.Empty;
            }

            string trimmed = mailbox.Trim();
            if (trimmed.StartsWith('<') && trimmed.EndsWith('>') && trimmed.Length >= 2) {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            return trimmed;
        }

        #endregion

        #region method IsTransientException

        private static bool IsTransientException(Exception ex)
        {
            return ex is TimeoutException ||
                   ex is OperationCanceledException ||
                   ex is SocketException ||
                   (ex.InnerException != null && IsTransientException(ex.InnerException));
        }

        #endregion
    }
}