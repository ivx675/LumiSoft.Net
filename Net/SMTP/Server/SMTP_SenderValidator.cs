using LumiSoft.Net.DNS;
using LumiSoft.Net.DNS.Client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LumiSoft.Net.SMTP.Server 
{
    /// <summary>
    /// Represents an exception encountered during SMTP sender validation.
    /// </summary>
    public class SMTP_ValidatorException : Exception
    {
        /// <summary>
        /// Gets a value indicating whether the failure is temporary (<c>true</c>, mapping to 4xx) 
        /// or permanent (<c>false</c>, mapping to 5xx).
        /// </summary>
        public bool IsTemporary { get; }

        /// <summary>
        /// Gets the descriptive error text for the validation failure.
        /// </summary>
        public string ErrorText { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_ValidatorException"/> class with the specified error state and message.
        /// </summary>
        /// <param name="isTemporary">Indicates whether the error is a transient/temporary issue (4xx).</param>
        /// <param name="errorText">The description of the validation failure.</param>
        public SMTP_ValidatorException(bool isTemporary, string errorText) : base(errorText)
        {
            IsTemporary = isTemporary;
            ErrorText = errorText;
        }
    }

    /// <summary>
    /// Provides strict SMTP sender validation via reverse DNS (PTR), SPF record evaluation, 
    /// and MX/Domain IP fallback authorization.
    /// </summary>
    public static class SMTP_SenderValidator
    {
        #region static method ValidateAsync

        /// <summary>
        /// Validates the remote sender using a strict PTR and SPF check. 
        /// For normal mail, validation targets the sender's domain; for null senders (&lt;&gt;), 
        /// it falls back to evaluating against the client's EHLO hostname.
        /// Throws an <see cref="SMTP_ValidatorException"/> upon failure.
        /// </summary>
        /// <param name="remoteIP">The IP address of the connecting client.</param>
        /// <param name="ehloName">The EHLO/HELO hostname provided by the client (used for null senders).</param>
        /// <param name="mailFrom">The MAIL FROM address (can be empty <c>&lt;&gt;</c> for bounce messages).</param>
        /// <returns>A task that represents the asynchronous validation operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="remoteIP"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="ehloName"/> is <c>null</c> or empty.</exception>
        /// <exception cref="SMTP_ValidatorException">Thrown if the sender fails PTR, SPF (including SoftFail <c>~all</c>), or MX authorization checks.</exception>
        public static async Task ValidateAsync(IPAddress remoteIP, string ehloName, string mailFrom)
        {
            if(remoteIP == null){
                throw new ArgumentNullException(nameof(remoteIP));
            }
            if(string.IsNullOrEmpty(ehloName)){
                throw new ArgumentException("EHLO name cannot be null or empty.", nameof(ehloName));
            }
            if(string.IsNullOrEmpty(mailFrom)){
                throw new ArgumentException("Mail From address cannot be null or empty.", nameof(mailFrom));
            }

            string domainToValidate;
            bool isNullSender = string.IsNullOrEmpty(mailFrom);

            if(isNullSender){
                // For bounce messages (<>), evaluate SPF/PTR against the EHLO domain
                domainToValidate = ehloName;
            }
            else{
                // For normal messages, extract the domain from the address
                int atIndex = mailFrom.IndexOf('@');
                domainToValidate = mailFrom.Substring(atIndex + 1);
            }

            // ==========================================
            // STEP 1: Strict PTR Check against EHLO
            // ==========================================
            await TryVerifyPtrAsync(remoteIP, ehloName);

            // ==========================================
            // STEP 2: Check SPF (or fallback to MX if none exists)
            // ==========================================
            bool spfRecordFound = await TryEvaluateSpfAsync(domainToValidate,remoteIP);

            // If no SPF record was published at all, fallback to strict MX/Domain IP authorization
            if(!spfRecordFound){
                await VerifyMxOrDomainFallbackAsync(domainToValidate, remoteIP);
            }
        }

        #endregion


        #region static method TryVerifyPtrAsync

        /// <summary>
        /// Performs a reverse DNS lookup on the remote IP and verifies that the resulting hostname matches the provided EHLO name.
        /// </summary>
        /// <param name="remoteIP">The remote client IP address.</param>
        /// <param name="ehloName">The EHLO name presented by the client.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task TryVerifyPtrAsync(IPAddress remoteIP, string ehloName)
        {
            try{
                IPHostEntry ptrEntry = await Dns.GetHostEntryAsync(remoteIP);
                bool ptrMatches = ptrEntry.HostName.Equals(ehloName, StringComparison.OrdinalIgnoreCase) ||
                    (ptrEntry.Aliases.Any(a => a.Equals(ehloName, StringComparison.OrdinalIgnoreCase)));

                if(!ptrMatches){
                    throw new SMTP_ValidatorException(false, $"Strict PTR check failed: Reverse DNS for '{remoteIP}' ('{ptrEntry.HostName}') does not match EHLO name '{ehloName}'.");
                }
            }
            catch(SMTP_ValidatorException){
                throw;
            }
            catch{
                // Transient DNS errors or missing PTR records are non-fatal; 
                // allow flow to fall through to SPF / MX authorization checks.
            }
        }

        #endregion

        #region static method TryEvaluateSpfAsync

        /// <summary>
        /// Checks if an SPF record exists for the domain and evaluates it against the remote IP.
        /// Returns true if an SPF record was found and evaluated (regardless of whether the IP passed or failed),
        /// or false if no SPF record exists at all.
        /// </summary>
        private static async Task<bool> TryEvaluateSpfAsync(string domain, IPAddress remoteIP)
        {
            return await EvaluateSpfRecursiveAsync(domain, remoteIP, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        #endregion

        #region static method EvaluateSpfRecursiveAsync

        /// <summary>
        /// Recursively evaluates SPF records, processing mechanisms and handling included domains or redirects.
        /// Throws an <see cref="SMTP_ValidatorException"/> if an SPF record is found but the IP is explicitly unauthorized.
        /// </summary>
        /// <returns>A task returning <c>true</c> if an SPF record was found and evaluated; <c>false</c> if no SPF record exists.</returns>
        private static async Task<bool> EvaluateSpfRecursiveAsync(string domain, IPAddress remoteIP, HashSet<string> visitedDomains)
        {
            if(!visitedDomains.Add(domain)){
                return false;
            }

            try{
                DNS_ServerResponse? response = await DNS_Client.Static.QueryAsync(domain, DNS_RecordType.TXT, 1000);
                if(response == null || response.ResponseCode != DNS_ResponseCode.NoError){
                    if(response != null && response.ResponseCode == DNS_ResponseCode.ServerFailure){
                        throw new SMTP_ValidatorException(true, $"Temporary DNS failure querying TXT records for '{domain}'.");
                    }
                    return false; // No SPF record found
                }

                
                string? spfRecord = null;
                foreach(var answer in response.Answers.TXT){
                    if(answer.Text.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)){
                        spfRecord = answer.Text;

                        break;
                    }
                }

                if(string.IsNullOrEmpty(spfRecord)){
                    return false; // No SPF record found
                }

                string[] parts = spfRecord.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach(var part in parts.Skip(1)){
                    if(part.StartsWith("redirect=", StringComparison.OrdinalIgnoreCase)){
                        string redirectDomain = part.Substring(9);
                        // A redirect record exists, evaluate the target domain's SPF
                        return await EvaluateSpfRecursiveAsync(redirectDomain, remoteIP, visitedDomains);
                    }
                    else if (part.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase)){
                        if (IpMatchesCidr(remoteIP, part.Substring(4))) return true;
                    }
                    else if (part.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase)){
                        if (IpMatchesCidr(remoteIP, part.Substring(4))) return true;
                    }
                    else if (part.StartsWith("include:", StringComparison.OrdinalIgnoreCase)){
                        string includedDomain = part.Substring(8);
                        if (await EvaluateSpfRecursiveAsync(includedDomain, remoteIP, visitedDomains)) return true;
                    }
                    else if (part.Equals("a", StringComparison.OrdinalIgnoreCase) || part.StartsWith("a:", StringComparison.OrdinalIgnoreCase)){
                        string targetDomain = part.StartsWith("a:") ? part.Substring(2) : domain;
                        if (await DomainHasIpAsync(targetDomain, remoteIP)) return true;
                    }
                    else if (part.Equals("mx", StringComparison.OrdinalIgnoreCase) || part.StartsWith("mx:", StringComparison.OrdinalIgnoreCase)){
                        string targetDomain = part.StartsWith("mx:") ? part.Substring(3) : domain;
                        if (await MxHasIpAsync(targetDomain, remoteIP)) return true;
                    }
                    else if (part.StartsWith("-all", StringComparison.OrdinalIgnoreCase)){
                        // SPF record exists, but IP matched hard fail (-all)
                        throw new SMTP_ValidatorException(false, $"SPF policy violation: IP '{remoteIP}' is not authorized by the SPF record for '{domain}'.");
                    }
                }

                // If we reached here, an SPF record exists, but the IP didn't match any positive rule 
                // (e.g., ~all, ?all, or all). Since an SPF record *was* found, reject immediately 
                // with a proper SPF failure instead of falling back to MX!
                throw new SMTP_ValidatorException(false, $"SPF policy violation: IP '{remoteIP}' is not authorized by the SPF record for '{domain}'.");
            }
            catch (SMTP_ValidatorException){
                throw; // Preserve original validation exception (temporary or permanent)
            }
            catch (Exception ex) when (IsTransientException(ex)){
                throw new SMTP_ValidatorException(true, $"Temporary DNS timeout/error during SPF evaluation for '{domain}': {ex.Message}");
            }
            catch (Exception ex){
                // If it's a generic unhandled exception during lookup, treat as a temporary DNS error
                throw new SMTP_ValidatorException(true, $"DNS processing error for SPF records on '{domain}': {ex.Message}");
            }
        }

        #endregion

        #region static method IpMatchesCidr

        /// <summary>
        /// Determines whether an IP address matches a given CIDR block notation (IPv4 or IPv6).
        /// </summary>
        /// <param name="address">The IP address to check.</param>
        /// <param name="cidrBlock">The CIDR block string (e.g., "192.168.1.0/24").</param>
        /// <returns><c>true</c> if the IP address matches the CIDR block; otherwise, <c>false</c>.</returns>
        private static bool IpMatchesCidr(IPAddress address, string cidrBlock)
        {
            try{
                var parts = cidrBlock.Split('/');
                string ipPart = parts[0];
                int prefixLength = -1;

                if(parts.Length > 1 && int.TryParse(parts[1], out int parsedPrefix)){
                    prefixLength = parsedPrefix;
                }

                if(!IPAddress.TryParse(ipPart, out IPAddress? networkIp)){
                    return false;
                }

                if(address.AddressFamily != networkIp.AddressFamily){
                    return false;
                }

                byte[] addressBytes = address.GetAddressBytes();
                byte[] networkBytes = networkIp.GetAddressBytes();

                if (prefixLength == -1){
                    return address.Equals(networkIp);
                }

                int maxPrefix = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128; // Note: AddressFamily.InterNetwork context
                if (prefixLength < 0 || prefixLength > maxPrefix){
                    return false;
                }

                int bytesToCheck = prefixLength / 8;
                int remainingBits = prefixLength % 8;

                for(int i = 0; i < bytesToCheck; i++){
                    if(addressBytes[i] != networkBytes[i]){
                        return false;
                    }
                }

                if(remainingBits > 0 && bytesToCheck < addressBytes.Length){
                    int mask = (byte)(0xFF << (8 - remainingBits));
                    if ((addressBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask)){
                        return false;
                    }
                }

                return true;
            }
            catch{
                return false;
            }
        }

        #endregion

        #region static method DomainHasIpAsync

        /// <summary>
        /// Resolves a domain's host addresses and checks if any match the specified remote IP address.
        /// Properly differentiates between transient DNS errors (throwing temporary exceptions) and permanent failures.
        /// </summary>
        /// <param name="domain">The target domain to resolve.</param>
        /// <param name="remoteIP">The remote IP address to verify against domain IPs.</param>
        /// <returns>A task returning <c>true</c> if a match is found; otherwise, <c>false</c>.</returns>
        private static async Task<bool> DomainHasIpAsync(string domain, IPAddress remoteIP)
        {
            try{
                IPAddress[] ips = await Dns.GetHostAddressesAsync(domain);
                return ips != null && ips.Any(ip => ip.Equals(remoteIP));
            }
            catch (Exception ex) when (IsTransientException(ex)){
                throw new SMTP_ValidatorException(true, $"Temporary DNS error resolving host addresses for '{domain}': {ex.Message}");
            }
            catch{
                // Permanent resolution failure (e.g., NXDOMAIN) -> domain has no matching IP
                return false;
            }
        }

        #endregion

        #region static method MxHasIpAsync

        /// <summary>
        /// Queries a domain's MX records, resolves their target host addresses, and checks if any match the specified remote IP address.
        /// </summary>
        /// <param name="domain">The target domain whose MX records should be queried.</param>
        /// <param name="remoteIP">The remote IP address to verify.</param>
        /// <returns>A task returning <c>true</c> if a match is found; otherwise, <c>false</c>.</returns>
        private static async Task<bool> MxHasIpAsync(string domain, IPAddress remoteIP)
        {
            try{
                DNS_ServerResponse? mxResponse = await DNS_Client.Static.QueryAsync(domain, DNS_RecordType.MX, 1000);
                if(mxResponse == null || mxResponse.ResponseCode != DNS_ResponseCode.NoError){
                    if(mxResponse != null && mxResponse.ResponseCode == DNS_ResponseCode.ServerFailure){
                        throw new SMTP_ValidatorException(true, $"Temporary DNS failure querying MX records for '{domain}'.");
                    }
                    return false;
                }
                
                foreach (var mx in mxResponse.Answers.MX){
                    if(!string.IsNullOrEmpty(mx.Host)){
                        try{
                            IPAddress[] mxIps = await Dns.GetHostAddressesAsync(mx.Host);
                            if (mxIps != null && mxIps.Any(ip => ip.Equals(remoteIP))) return true;
                        }
                        catch (Exception ex) when (IsTransientException(ex)){
                            throw new SMTP_ValidatorException(true, $"Temporary DNS error resolving MX host '{mx.Host}': {ex.Message}");
                        }
                        catch { }
                    }
                }
            }
            catch (SMTP_ValidatorException){
                throw;
            }
            catch (Exception ex) when (IsTransientException(ex)){
                throw new SMTP_ValidatorException(true, $"Temporary DNS timeout/error querying MX records for '{domain}': {ex.Message}");
            }
            catch { }

            return false;
        }

        #endregion

        #region static method VerifyMxOrDomainFallbackAsync

        /// <summary>
        /// Validates fallback authorization when no SPF record is present, checking both the domain's own IP addresses 
        /// and its MX record host IP addresses against the remote client IP.
        /// </summary>
        /// <param name="senderDomain">The sender's domain extracted from MAIL FROM.</param>
        /// <param name="remoteIP">The remote client IP address.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task VerifyMxOrDomainFallbackAsync(string senderDomain, IPAddress remoteIP)
        {
            var authorizedIPs = new HashSet<IPAddress>();
            bool dnsQuerySucceeded = false;

            // 1. Resolve domain's own IP addresses
            try{
                IPAddress[] domainIps = await Dns.GetHostAddressesAsync(senderDomain);
                foreach(var ip in domainIps){
                    authorizedIPs.Add(ip);
                }
                dnsQuerySucceeded = true;
            }
            catch (Exception ex) when (IsTransientException(ex)){
                throw new SMTP_ValidatorException(true, $"Temporary DNS error resolving host addresses for '{senderDomain}': {ex.Message}");
            }
            catch { }

            // 2. Resolve domain's MX records and their IP addresses
            try{
                DNS_ServerResponse? mxResponse = await DNS_Client.Static.QueryAsync(senderDomain, DNS_RecordType.MX, 1000);
                if(mxResponse != null && mxResponse.ResponseCode == DNS_ResponseCode.NoError){
                    foreach(var mx in mxResponse.Answers.MX){
                        if(!string.IsNullOrEmpty(mx.Host)){
                            try{
                                IPAddress[] mxIps = await Dns.GetHostAddressesAsync(mx.Host);
                                foreach (var ip in mxIps){
                                    authorizedIPs.Add(ip);
                                }
                            }
                            catch { }
                        }
                    }
                    dnsQuerySucceeded = true;
                }
                else if (mxResponse != null && mxResponse.ResponseCode == DNS_ResponseCode.ServerFailure){
                    throw new SMTP_ValidatorException(true, $"Temporary DNS failure querying MX records for '{senderDomain}'.");
                }
            }
            catch (SMTP_ValidatorException){
                throw;
            }
            catch (Exception ex) when (IsTransientException(ex)){
                throw new SMTP_ValidatorException(true, $"Temporary DNS timeout/error querying MX records for '{senderDomain}': {ex.Message}");
            }
            catch { }

            // 3. Verify if remoteIP matches any authorized IP
            bool isAuthorized = authorizedIPs.Any(ip => ip.Equals(remoteIP));
            if(!isAuthorized){
                bool isTemp = !dnsQuerySucceeded;
                string error = isTemp 
                    ? $"Temporary DNS failure: Unable to resolve host or MX records for '{senderDomain}'."
                    : $"Sender validation failed: No valid SPF record found, and remote IP '{remoteIP}' is not listed in domain host or MX records for '{senderDomain}'.";
            
                throw new SMTP_ValidatorException(isTemp, error);
            }
        }

        #endregion

        #region static method IsTransientException

        /// <summary>
        /// Determines whether an exception represents a transient network or timeout error.
        /// </summary>
        /// <param name="ex">The exception to evaluate.</param>
        /// <returns><c>true</c> if the exception is transient; otherwise, <c>false</c>.</returns>
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
