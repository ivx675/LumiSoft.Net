using LumiSoft.Net.DNS;
using LumiSoft.Net.DNS.Client;
using LumiSoft.Net.IO;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Provides RFC 6376 compliant DomainKeys Identified Mail (DKIM) signing and verification
    /// using zero-string-allocation byte-level canonicalization and streaming body hashing.
    /// </summary>
    public static class DkimService
    {
        /// <summary>
        /// Represents a parsed RFC 5322 message header preserving its raw field name bytes, lowercase ASCII name,
        /// and raw field value bytes (including original folding).
        /// </summary>
        /// <param name="RawNameBytes">The exact raw ASCII bytes of the header field name before the colon.</param>
        /// <param name="NameLower">The lowercase ASCII header field name used for case-insensitive matching.</param>
        /// <param name="RawValueBytes">The exact raw bytes of the header field value after the colon, preserving folding.</param>
        private sealed record HeaderField(byte[] RawNameBytes, string NameLower, byte[] RawValueBytes);

        /// <summary>
        /// Default ordered list of RFC 5322 header fields signed when no custom header list is specified.
        /// </summary>
        private static readonly string[] DefaultSignedHeaders =
        {
            "from",
            "to",
            "cc",
            "subject",
            "date",
            "message-id",
            "reply-to",
            "in-reply-to",
            "references",
            "mime-version",
            "content-type",
            "content-transfer-encoding"
        };

        #region class DkimVerificationResult

        /// <summary>
        /// Represents the outcome of a DomainKeys Identified Mail (DKIM) signature verification operation.
        /// Contains the verification status and, when verification fails, a human-readable explanation
        /// describing the reason for the failure.
        /// </summary>
        public sealed class DkimVerificationResult
        {
            /// <summary>
            /// Gets a value indicating whether the DKIM signature was successfully verified.
            /// </summary>
            public bool IsValid { get; init; }

            /// <summary>
            /// Gets the verification failure reason when <see cref="IsValid"/> is <see langword="false"/>.
            /// Returns <see langword="null"/> when verification succeeds.
            /// </summary>
            public string? FailureReason { get; init; }

            /// <summary>
            /// Creates a successful DKIM verification result.
            /// </summary>
            /// <returns>A <see cref="DkimVerificationResult"/> indicating successful verification.</returns>
            internal static DkimVerificationResult Success() =>
                new()
                {
                    IsValid = true
                };

            /// <summary>
            /// Creates a failed DKIM verification result with the specified diagnostic message.
            /// </summary>
            /// <param name="reason">
            /// A human-readable explanation describing why DKIM verification failed.
            /// </param>
            /// <returns>A <see cref="DkimVerificationResult"/> representing a failed verification.</returns>
            internal static DkimVerificationResult Fail(string reason) =>
                new()
                {
                    IsValid = false,
                    FailureReason = reason
                };
        }

        #endregion


        #region method CreateSignatureHeaderAsync

        /// <summary>
        /// Computes a complete <c>DKIM-Signature</c> header value (including the Base64-encoded <c>b=</c> signature)
        /// for the provided message stream using an RSA private key and <c>relaxed/relaxed</c> canonicalization.
        /// </summary>
        /// <param name="messageStream">
        /// The raw RFC 5322 email message stream. Must be readable and positioned at the start of the message headers.
        /// The stream is left open upon completion, and its position is restored if <see cref="Stream.CanSeek"/> is <see langword="true"/>.
        /// </param>
        /// <param name="domain">The signing domain identifier (<c>d=</c> tag), e.g., <c>"example.com"</c>.</param>
        /// <param name="selector">The DKIM selector (<c>s=</c> tag) identifying the public key record in DNS, e.g., <c>"default"</c>.</param>
        /// <param name="privateKeyPem">The PEM-encoded RSA private key (<c>-----BEGIN PRIVATE KEY-----</c> or <c>-----BEGIN RSA PRIVATE KEY-----</c>).</param>
        /// <param name="headersToSign">
        /// Optional ordered list of header field names to sign (<c>h=</c> tag). If <see langword="null"/> or empty,
        /// <see cref="DefaultSignedHeaders"/> is used.
        /// </param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task whose result is the complete formatted
        /// <c>DKIM-Signature</c> header value, including all DKIM tags and the
        /// Base64-encoded <c>b=</c> signature value, but excluding the
        /// <c>"DKIM-Signature:"</c> header field name and colon.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageStream"/>, <paramref name="domain"/>, <paramref name="selector"/>, or <paramref name="privateKeyPem"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="messageStream"/> is not readable, if required string parameters are empty, or if the <c>From</c> header is missing.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Thrown if the PEM private key cannot be imported or signing fails.
        /// </exception>
        /// <remarks>
        /// The returned value is suitable for insertion as the value of a
        /// <c>DKIM-Signature</c> RFC 5322 header field, for example:
        ///
        /// DKIM-Signature: v=1; a=rsa-sha256; ...
        /// </remarks>
        public static async Task<string> CreateSignatureHeaderAsync(
            Stream messageStream,
            string domain,
            string selector,
            string privateKeyPem,
            IEnumerable<string>? headersToSign = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(messageStream);
            if (!messageStream.CanRead)
                throw new ArgumentException("Message stream must be readable.", nameof(messageStream));
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Signing domain cannot be empty.", nameof(domain));
            if (string.IsNullOrWhiteSpace(selector))
                throw new ArgumentException("DKIM selector cannot be empty.", nameof(selector));
            if (string.IsNullOrWhiteSpace(privateKeyPem))
                throw new ArgumentException("Private key PEM cannot be empty.", nameof(privateKeyPem));

            string normalizedDomain = domain.Trim().ToLowerInvariant();
            string normalizedSelector = selector.Trim().ToLowerInvariant();
            const string algorithm = "rsa-sha256";

            long originalPosition = messageStream.CanSeek ? messageStream.Position : -1;

            try {
                using var reader = new SmartStream(messageStream, false);

                // 1. Read raw headers without converting values to string; leaves reader at first byte of body.
                List<HeaderField> headers = await ReadHeadersAsync(reader, cancellationToken).ConfigureAwait(false);

                // 2. Determine which headers actually exist on the message (From is mandatory per RFC 6376 §5.4.1).
                var requestedHeaders = (headersToSign != null && headersToSign.Any()
                        ? headersToSign
                        : DefaultSignedHeaders)
                    .Select(h => h.Trim().ToLowerInvariant())
                    .Where(h => !string.IsNullOrEmpty(h) && h != "dkim-signature")
                    .ToList();

                if (!requestedHeaders.Contains("from")) {
                    requestedHeaders.Insert(0, "from");
                }

                var availableCounts = headers
                    .GroupBy(h => h.NameLower, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                if (!availableCounts.TryGetValue("from", out int fromCount) || fromCount == 0) {
                    throw new ArgumentException("Message must contain a 'From' header to be DKIM signed.", nameof(messageStream));
                }

                var effectiveHeaders = new List<string>();
                foreach (string hName in requestedHeaders) {
                    if (availableCounts.TryGetValue(hName, out int remaining) && remaining > 0) {
                        effectiveHeaders.Add(hName);
                        availableCounts[hName] = remaining - 1;
                    }
                }

                string hTag = string.Join(":", effectiveHeaders);

                // 3. Stream raw body bytes directly into SHA-256 IncrementalHash (O(1) memory).
                string bhBase64 = await ComputeStreamingBodyHashBase64Async(
                    reader,
                    isRelaxedBody: true,
                    HashAlgorithmName.SHA256,
                    cancellationToken).ConfigureAwait(false);

                // 4. Build unsigned DKIM-Signature template with empty b= and compute header digest.
                string unsignedDkimValue =
                    $"v=1; a={algorithm}; c=relaxed/relaxed; d={normalizedDomain}; s={normalizedSelector};\r\n" +
                    $" h={hTag};\r\n bh={bhBase64};\r\n b=";

                byte[] headerDigest = ComputeHeaderDigest(
                    headers,
                    effectiveHeaders,
                    Encoding.ASCII.GetBytes(unsignedDkimValue),
                    isRelaxedHeader: true,
                    HashAlgorithmName.SHA256);

                // 5. Sign header digest with RSA PKCS#1 v1.5 + SHA-256.
                using RSA rsa = RSA.Create();
                rsa.ImportFromPem(privateKeyPem);

                byte[] signatureBytes = rsa.SignHash(
                    headerDigest,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                string foldedSignature = FoldBase64(Convert.ToBase64String(signatureBytes));

                return $"{unsignedDkimValue}{foldedSignature}";
            }
            finally {
                if (originalPosition >= 0 && messageStream.CanSeek) {
                    messageStream.Position = originalPosition;
                }
            }
        }

        #endregion

        #region method VerifySignatureAsync

        /// <summary>
        /// Verifies the <c>DKIM-Signature</c> headers in
        /// <paramref name="signedMessageStream"/> against the public key
        /// retrieved from DNS and returns the verification result.
        /// Per RFC 6376 §6.1, if multiple <c>DKIM-Signature</c> headers are present, verification succeeds
        /// as soon as at least one signature passes cryptographic validation.
        /// </summary>
        /// <param name="signedMessageStream">
        /// The raw RFC 5322 signed email message stream. Must be readable and positioned at the start of the headers.
        /// The stream is left open upon completion, and its position is restored if <see cref="Stream.CanSeek"/> is <see langword="true"/>.
        /// </param>
        /// <param name="allowSha1">
        /// When <see langword="true"/>, allows verifying legacy <c>a=rsa-sha1</c> signatures. Defaults to <see langword="false"/>
        /// per RFC 8301.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// A task whose result contains the outcome of the DKIM verification operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="signedMessageStream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="signedMessageStream"/> is not readable.
        /// </exception>
        public static async Task<DkimVerificationResult> VerifySignatureAsync(
            Stream signedMessageStream,
            bool allowSha1 = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(signedMessageStream);
            if (!signedMessageStream.CanRead)
                throw new ArgumentException("Signed message stream must be readable.", nameof(signedMessageStream));

            long originalPosition = signedMessageStream.CanSeek ? signedMessageStream.Position : -1;

            try {
                using var reader = new SmartStream(signedMessageStream, false);

                // 1. Read raw headers and collect all DKIM-Signature headers (top-to-bottom per RFC 6376 §6.1).
                List<HeaderField> headers = await ReadHeadersAsync(reader, cancellationToken).ConfigureAwait(false);

                List<HeaderField> dkimHeaders = headers
                    .Where(h => string.Equals(h.NameLower, "dkim-signature", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (dkimHeaders.Count == 0)
                    return DkimVerificationResult.Fail("DKIM verification failed: no DKIM-Signature header found.");

                long bodyStartPosition = signedMessageStream.CanSeek ? signedMessageStream.Position : -1;
                var bodyHashCache = new Dictionary<(bool IsRelaxedBody, HashAlgorithmName Algorithm), string>();
                var failureReasons = new List<string>(dkimHeaders.Count);

                // 2. Evaluate each DKIM-Signature header; pass as soon as any signature succeeds (RFC 6376 §6.1).
                for (int sigIndex = 0; sigIndex < dkimHeaders.Count; sigIndex++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    HeaderField dkimHeader = dkimHeaders[sigIndex];

                    if (dkimHeader.RawValueBytes.Length == 0) {
                        failureReasons.Add("empty DKIM-Signature header.");
                        continue;
                    }

                    string unfoldedDkimText = Encoding.Latin1.GetString(dkimHeader.RawValueBytes)
                        .Replace("\r\n", " ")
                        .Replace("\n", " ");
                    Dictionary<string, string> tags = ParseDkimTags(unfoldedDkimText);

                    if (tags.TryGetValue("v", out string? version) && version.Trim() != "1") {
                        failureReasons.Add($"unsupported DKIM version '{version}'.");
                        continue;
                    }

                    // Validate signing algorithm (a=). Skip unsupported algorithms so remaining signatures can still be checked.
                    string algorithm = tags.TryGetValue("a", out string? aVal) && !string.IsNullOrWhiteSpace(aVal)
                        ? aVal.Trim().ToLowerInvariant()
                        : "rsa-sha256";

                    HashAlgorithmName hashAlgorithm;
                    if (algorithm == "rsa-sha256") {
                        hashAlgorithm = HashAlgorithmName.SHA256;
                    }
                    else if (algorithm == "rsa-sha1" && allowSha1) {
                        hashAlgorithm = HashAlgorithmName.SHA1;
                    }
                    else if (algorithm == "rsa-sha1") {
                        failureReasons.Add("DKIM algorithm 'rsa-sha1' is deprecated (RFC 8301) and not enabled.");
                        continue;
                    }
                    else {
                        failureReasons.Add($"unsupported DKIM signing algorithm '{algorithm}'.");
                        continue;
                    }

                    // 3. Validate mandatory DKIM tags (d=, s=, h=, bh=, b=).
                    if (!tags.TryGetValue("d", out string? domain) || string.IsNullOrWhiteSpace(domain)) {
                        failureReasons.Add("missing 'd=' domain tag.");
                        continue;
                    }
                    if (!tags.TryGetValue("s", out string? selector) || string.IsNullOrWhiteSpace(selector)) {
                        failureReasons.Add("missing 's=' selector tag.");
                        continue;
                    }
                    if (!tags.TryGetValue("h", out string? hVal) || string.IsNullOrWhiteSpace(hVal)) {
                        failureReasons.Add("missing 'h=' signed headers tag.");
                        continue;
                    }
                    if (!tags.TryGetValue("bh", out string? bhExpectedRaw) || string.IsNullOrWhiteSpace(bhExpectedRaw)) {
                        failureReasons.Add("missing 'bh=' body hash tag.");
                        continue;
                    }
                    if (!tags.TryGetValue("b", out string? bSignatureRaw) || string.IsNullOrWhiteSpace(bSignatureRaw)) {
                        failureReasons.Add("missing 'b=' signature tag.");
                        continue;
                    }

                    domain = domain.Trim().ToLowerInvariant();
                    selector = selector.Trim().ToLowerInvariant();
                    string sigLabel = $"[{selector}._domainkey.{domain}]";

                    // Check optional expiration timestamp (x=).
                    if (tags.TryGetValue("x", out string? xVal) && long.TryParse(xVal.Trim(), out long expiresUnix)) {
                        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix) {
                            failureReasons.Add($"{sigLabel} signature has expired (x=).");
                            continue;
                        }
                    }

                    // 4. Parse canonicalization mode (c=header/body, defaults to simple/simple per RFC 6376 §3.5).
                    (bool isRelaxedHeader, bool isRelaxedBody) = ParseCanonicalizationTag(
                        tags.TryGetValue("c", out string? cVal) ? cVal : null);

                    // 5. Stream body bytes directly into IncrementalHash (cached per canonicalization/hash pair) and verify bh= before DNS lookup.
                    var cacheKey = (isRelaxedBody, hashAlgorithm);
                    if (!bodyHashCache.TryGetValue(cacheKey, out string? computedBhBase64)) {
                        if (bodyHashCache.Count == 0) {
                            computedBhBase64 = await ComputeStreamingBodyHashBase64Async(
                                reader,
                                isRelaxedBody,
                                hashAlgorithm,
                                cancellationToken).ConfigureAwait(false);
                        }
                        else {
                            if (!signedMessageStream.CanSeek || bodyStartPosition < 0) {
                                failureReasons.Add($"{sigLabel} stream is non-seekable for multiple body canonicalizations.");
                                continue;
                            }
                            signedMessageStream.Position = bodyStartPosition;
                            using var bodyReader = new SmartStream(signedMessageStream, false);
                            computedBhBase64 = await ComputeStreamingBodyHashBase64Async(
                                bodyReader,
                                isRelaxedBody,
                                hashAlgorithm,
                                cancellationToken).ConfigureAwait(false);
                        }
                        bodyHashCache[cacheKey] = computedBhBase64;
                    }

                    string normalizedExpectedBh = RemoveAllWhitespace(bhExpectedRaw);
                    if (!string.Equals(computedBhBase64, normalizedExpectedBh, StringComparison.Ordinal)) {
                        failureReasons.Add($"{sigLabel} body hash (bh=) mismatch.");
                        continue;
                    }

                    // 6. Reconstruct exact DKIM-Signature header value with b= emptied (preserving raw folding/tags).
                    byte[] unsignedDkimRawBytes = StripBTagValueBytes(dkimHeader.RawValueBytes);

                    var signedHeaderNames = hVal
                        .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(h => h.ToLowerInvariant())
                        .ToList();

                    var candidateHeaders = headers
                        .Where(h => !ReferenceEquals(h, dkimHeader))
                        .ToList();

                    byte[] headerDigest = ComputeHeaderDigest(
                        candidateHeaders,
                        signedHeaderNames,
                        unsignedDkimRawBytes,
                        isRelaxedHeader,
                        hashAlgorithm);

                    // 7. Resolve public key from DNS and verify RSA signature.
                    string? dnsRecordOrPem =
                        await ResolveDkimPublicKeyFromDnsAsync(selector, domain, cancellationToken).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(dnsRecordOrPem)) {
                        failureReasons.Add($"{sigLabel} no public key found in DNS.");
                        continue;
                    }

                    string publicKeyPem = ExtractPemFromDnsRecord(dnsRecordOrPem);
                    if (string.IsNullOrWhiteSpace(publicKeyPem)) {
                        failureReasons.Add($"{sigLabel} DNS record contained an empty or revoked 'p=' public key.");
                        continue;
                    }

                    try {
                        byte[] claimedSignatureBytes = Convert.FromBase64String(RemoveAllWhitespace(bSignatureRaw));

                        using RSA rsa = RSA.Create();
                        rsa.ImportFromPem(publicKeyPem);

                        bool isValid = rsa.VerifyHash(
                            headerDigest,
                            claimedSignatureBytes,
                            hashAlgorithm,
                            RSASignaturePadding.Pkcs1);

                        if (isValid) {
                            return DkimVerificationResult.Success();
                        }

                        failureReasons.Add($"{sigLabel} RSA cryptographic signature mismatch.");
                    }
                    catch (FormatException) {
                        failureReasons.Add($"{sigLabel} 'b=' signature is not valid Base64.");
                    }
                    catch (CryptographicException ex) {
                        failureReasons.Add($"{sigLabel} cryptographic verification error ({ex.Message}).");
                    }
                }

                return DkimVerificationResult.Fail($"DKIM verification failed: {string.Join(" | ", failureReasons)}");
            }
            finally {
                if (originalPosition >= 0 && signedMessageStream.CanSeek) {
                    signedMessageStream.Position = originalPosition;
                }
            }
        }

        #endregion


        #region method ReadHeadersAsync

        /// <summary>
        /// Reads the RFC 5322 header section from <paramref name="reader"/> as raw byte arrays until the blank separator line,
        /// leaving <paramref name="reader"/> positioned at the first byte of the message body.
        /// </summary>
        /// <param name="reader">The <see cref="SmartStream"/> positioned at the start of the message headers.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of parsed <see cref="HeaderField"/> instances preserving exact raw field name and value bytes.</returns>
        private static async Task<List<HeaderField>> ReadHeadersAsync(SmartStream reader, CancellationToken cancellationToken)
        {
            var headers = new List<HeaderField>();

            byte[]? currentNameBytes = null;
            string? currentNameLower = null;
            using var currentRawValue = new MemoryStream();
            byte[] lineBuf = ArrayPool<byte>.Shared.Rent(16384);

            try {
                while (true) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await reader.ReadLineAsync(
                        lineBuf,
                        SizeExceededAction.ThrowException,
                        cancellationToken).ConfigureAwait(false);

                    if (result.BytesInBuffer == 0) {
                        if (currentNameBytes != null && currentNameLower != null) {
                            headers.Add(new HeaderField(currentNameBytes, currentNameLower, currentRawValue.ToArray()));
                        }

                        return headers;
                    }

                    ReadOnlySpan<byte> lineBytes = lineBuf.AsSpan(0, result.LineBytesInBuffer);
                    ReadOnlySpan<byte> lineEnding = lineBuf.AsSpan(
                        result.LineBytesInBuffer,
                        result.BytesInBuffer - result.LineBytesInBuffer);

                    if (lineBytes.Length == 0) {
                        // Blank line marks the end of the RFC 5322 header section.
                        if (currentNameBytes != null && currentNameLower != null) {
                            headers.Add(new HeaderField(currentNameBytes, currentNameLower, currentRawValue.ToArray()));
                        }
                        return headers;
                    }

                    if ((lineBytes[0] == (byte)' ' || lineBytes[0] == (byte)'\t') && currentNameBytes != null) {
                        // Folded header continuation line (FWS): preserve exact line ending + leading WSP bytes.
                        currentRawValue.Write(lineEnding);
                        currentRawValue.Write(lineBytes);
                    }
                    else {
                        if (currentNameBytes != null && currentNameLower != null) {
                            headers.Add(new HeaderField(currentNameBytes, currentNameLower, currentRawValue.ToArray()));
                        }

                        int colonIndex = lineBytes.IndexOf((byte)':');
                        if (colonIndex <= 0) {
                            currentNameBytes = null;
                            currentNameLower = null;
                            currentRawValue.SetLength(0);
                            continue;
                        }

                        currentNameBytes = lineBytes.Slice(0, colonIndex).ToArray();
                        currentNameLower = Encoding.ASCII.GetString(currentNameBytes).ToLowerInvariant();

                        currentRawValue.SetLength(0);
                        currentRawValue.Write(lineBytes.Slice(colonIndex + 1));
                    }
                }
            }
            finally {
                ArrayPool<byte>.Shared.Return(lineBuf);
            }
        }

        #endregion

        #region method ComputeHeaderDigest

        /// <summary>
        /// Computes the cryptographic digest over the raw bytes of signed headers (selected bottom-up per RFC 6376 §5.4)
        /// followed by the canonicalized <c>DKIM-Signature</c> header bytes without a trailing CRLF.
        /// </summary>
        /// <param name="messageHeaders">All candidate message headers (excluding the <c>DKIM-Signature</c> being verified).</param>
        /// <param name="signedHeaderNames">Lowercase header field names listed in the <c>h=</c> tag.</param>
        /// <param name="unsignedDkimRawValueBytes">Raw bytes of the <c>DKIM-Signature</c> header value with the <c>b=</c> value removed.</param>
        /// <param name="isRelaxedHeader"><see langword="true"/> for <c>relaxed</c> header canonicalization; <see langword="false"/> for <c>simple</c>.</param>
        /// <param name="hashAlgorithm">The cryptographic hash algorithm.</param>
        /// <returns>The raw cryptographic digest bytes to sign or verify.</returns>
        private static byte[] ComputeHeaderDigest(
            IReadOnlyList<HeaderField> messageHeaders,
            IReadOnlyList<string> signedHeaderNames,
            ReadOnlySpan<byte> unsignedDkimRawValueBytes,
            bool isRelaxedHeader,
            HashAlgorithmName hashAlgorithm)
        {
            var usedIndices = new HashSet<int>();
            using IncrementalHash hasher = IncrementalHash.CreateHash(hashAlgorithm);

            foreach (string targetHeaderLower in signedHeaderNames) {
                int foundIndex = -1;
                for (int i = messageHeaders.Count - 1; i >= 0; i--) {
                    if (!usedIndices.Contains(i) &&
                        string.Equals(messageHeaders[i].NameLower, targetHeaderLower, StringComparison.OrdinalIgnoreCase))
                    {
                        foundIndex = i;
                        break;
                    }
                }

                // Per RFC 6376 §5.4.2, non-existent headers listed in h= are treated as empty (skipped).
                if (foundIndex < 0)
                    continue;

                usedIndices.Add(foundIndex);
                HeaderField header = messageHeaders[foundIndex];
                AppendCanonicalizedHeaderBytes(
                    hasher,
                    header.RawNameBytes,
                    header.RawValueBytes,
                    isRelaxedHeader,
                    appendCrlf: true);
            }

            // Append DKIM-Signature header last, WITHOUT trailing CRLF (RFC 6376 §3.7).
            AppendCanonicalizedHeaderBytes(
                hasher,
                "DKIM-Signature"u8,
                unsignedDkimRawValueBytes,
                isRelaxedHeader,
                appendCrlf: false);

            return hasher.GetHashAndReset();
        }

        #endregion

        #region method ComputeStreamingBodyHashBase64Async

        /// <summary>
        /// Streams raw body bytes from <paramref name="reader"/> directly into <see cref="IncrementalHash"/>
        /// in O(1) memory without any string allocation, according to RFC 6376 §3.4.3 (<c>simple</c>) or §3.4.4 (<c>relaxed</c>).
        /// </summary>
        /// <param name="reader">The <see cref="SmartStream"/> positioned at the first byte of the message body.</param>
        /// <param name="isRelaxedBody"><see langword="true"/> for <c>relaxed</c> body canonicalization; <see langword="false"/> for <c>simple</c>.</param>
        /// <param name="hashAlgorithm">The cryptographic hash algorithm (e.g., <see cref="HashAlgorithmName.SHA256"/>).</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The Base64-encoded canonicalized body digest (<c>bh=</c> value).</returns>
        private static async Task<string> ComputeStreamingBodyHashBase64Async(
            SmartStream reader,
            bool isRelaxedBody,
            HashAlgorithmName hashAlgorithm,
            CancellationToken cancellationToken)
        {
            byte[] crlf = [(byte)'\r',(byte)'\n'];

            using IncrementalHash hasher = IncrementalHash.CreateHash(hashAlgorithm);
            byte[] lineBuf = ArrayPool<byte>.Shared.Rent(65536);
            byte[]? canonicalBuf = isRelaxedBody
                ? ArrayPool<byte>.Shared.Rent(65536)
                : null;

            int pendingEmptyLines = 0;
            bool hasAnyNonEmptyLine = false;

            try {
                while (true) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await reader.ReadLineAsync(
                        lineBuf,
                        SizeExceededAction.ThrowException,
                        cancellationToken).ConfigureAwait(false);

                    if (result.BytesInBuffer == 0)
                        break;

                    ReadOnlySpan<byte> lineBytes = lineBuf.AsSpan(0, result.LineBytesInBuffer);
                    ReadOnlySpan<byte> canonicalLine;

                    if (isRelaxedBody) {
                        ReadOnlySpan<byte> trimmed = TrimEndWsp(lineBytes);
                        if (trimmed.Length == 0) {
                            canonicalLine = ReadOnlySpan<byte>.Empty;
                        }
                        else {
                            if (canonicalBuf!.Length < trimmed.Length) {
                                ArrayPool<byte>.Shared.Return(canonicalBuf);
                                canonicalBuf = ArrayPool<byte>.Shared.Rent(trimmed.Length);
                            }

                            int written = CanonicalizeRelaxedBodyLineBytes(trimmed, canonicalBuf);
                            canonicalLine = canonicalBuf.AsSpan(0, written);
                        }
                    }
                    else {
                        canonicalLine = lineBytes;
                    }

                    if (canonicalLine.Length == 0) {
                        // Defer trailing empty lines; only emit CRLFs if a subsequent non-empty line appears.
                        pendingEmptyLines++;
                        continue;
                    }

                    while (pendingEmptyLines > 0) {
                        hasher.AppendData(crlf);
                        pendingEmptyLines--;
                    }

                    hasAnyNonEmptyLine = true;
                    hasher.AppendData(canonicalLine);
                    hasher.AppendData(crlf);
                }

                // RFC 6376 §3.4.3 & §3.4.4: An empty body canonicalizes to empty in relaxed mode,
                // or a single CRLF in simple mode.
                if (!hasAnyNonEmptyLine && !isRelaxedBody) {
                    hasher.AppendData(crlf);
                }

                byte[] hashBytes = hasher.GetHashAndReset();

                return Convert.ToBase64String(hashBytes);
            }
            finally {
                ArrayPool<byte>.Shared.Return(lineBuf);
                if (canonicalBuf != null) {
                    ArrayPool<byte>.Shared.Return(canonicalBuf);
                }
            }
        }

        #endregion

        #region method AppendCanonicalizedHeaderBytes

        /// <summary>
        /// Appends a single canonicalized header field directly as bytes to <paramref name="hasher"/>
        /// according to RFC 6376 §3.4.1 (<c>simple</c>) or §3.4.2 (<c>relaxed</c>).
        /// </summary>
        /// <param name="hasher">The incremental hash accumulator.</param>
        /// <param name="rawNameBytes">Raw ASCII bytes of the header field name.</param>
        /// <param name="rawValueBytes">Raw bytes of the header field value (after the colon).</param>
        /// <param name="isRelaxedHeader"><see langword="true"/> for <c>relaxed</c>; <see langword="false"/> for <c>simple</c>.</param>
        /// <param name="appendCrlf"><see langword="true"/> to append a trailing <c>\r\n</c>; <see langword="false"/> for the final <c>DKIM-Signature</c> header.</param>
        private static void AppendCanonicalizedHeaderBytes(
            IncrementalHash hasher,
            ReadOnlySpan<byte> rawNameBytes,
            ReadOnlySpan<byte> rawValueBytes,
            bool isRelaxedHeader,
            bool appendCrlf)
        {
            ReadOnlySpan<byte> colon = ":"u8;
            ReadOnlySpan<byte> crlf = "\r\n"u8;

            if (!isRelaxedHeader) {
                hasher.AppendData(rawNameBytes);
                hasher.AppendData(colon);
                hasher.AppendData(rawValueBytes);
                if (appendCrlf)
                    hasher.AppendData(crlf);
                return;
            }

            // Relaxed header canonicalization (RFC 6376 §3.4.2):
            // 1. Lowercase ASCII header field name and trim trailing WSP before ':'.
            ReadOnlySpan<byte> trimmedName = TrimEndWsp(rawNameBytes);
            byte[] nameBuf = ArrayPool<byte>.Shared.Rent(trimmedName.Length);
            byte[] valBuf = ArrayPool<byte>.Shared.Rent(rawValueBytes.Length);

            try {
                for (int i = 0; i < trimmedName.Length; i++) {
                    byte b = trimmedName[i];
                    nameBuf[i] = (b >= (byte)'A' && b <= (byte)'Z') ? (byte)(b + 32) : b;
                }

                // 2. Unfold CRLF/LF and collapse runs of WSP (0x20 / 0x09) into single 0x20, trimming leading/trailing WSP.
                int valLen = CanonicalizeRelaxedHeaderValueBytes(rawValueBytes, valBuf);

                hasher.AppendData(nameBuf.AsSpan(0, trimmedName.Length));
                hasher.AppendData(colon);
                hasher.AppendData(valBuf.AsSpan(0, valLen));
                if (appendCrlf)
                    hasher.AppendData(crlf);
            }
            finally {
                ArrayPool<byte>.Shared.Return(nameBuf);
                ArrayPool<byte>.Shared.Return(valBuf);
            }
        }

        #endregion

        #region method CanonicalizeRelaxedBodyLineBytes

        /// <summary>
        /// Canonicalizes a single body line for RFC 6376 §3.4.4 (<c>relaxed</c> body canonicalization)
        /// by collapsing every contiguous run of ASCII space (<c>0x20</c>) and tab (<c>0x09</c>) bytes into a single <c>0x20</c> byte.
        /// </summary>
        /// <param name="sourceLine">The right-trimmed body line byte span.</param>
        /// <param name="destination">The destination span receiving the canonicalized body line bytes.</param>
        /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
        private static int CanonicalizeRelaxedBodyLineBytes(ReadOnlySpan<byte> sourceLine, Span<byte> destination)
        {
            int written = 0;
            bool inWhitespace = false;

            for (int i = 0; i < sourceLine.Length; i++) {
                byte b = sourceLine[i];
                if (b == (byte)' ' || b == (byte)'\t') {
                    if (!inWhitespace) {
                        destination[written++] = (byte)' ';
                        inWhitespace = true;
                    }
                }
                else {
                    destination[written++] = b;
                    inWhitespace = false;
                }
            }

            return written;
        }

        #endregion

        #region method CanonicalizeRelaxedHeaderValueBytes

        /// <summary>
        /// Unfolds line breaks (<c>\r</c>/<c>\n</c>), collapses contiguous WSP into a single <c>0x20</c>,
        /// and strips leading and trailing WSP bytes per RFC 6376 §3.4.2.
        /// </summary>
        /// <param name="source">The raw header value bytes.</param>
        /// <param name="destination">The destination buffer receiving canonicalized header value bytes.</param>
        /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
        private static int CanonicalizeRelaxedHeaderValueBytes(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int written = 0;
            bool inWhitespace = false;
            bool seenNonWhitespace = false;

            for (int i = 0; i < source.Length; i++) {
                byte b = source[i];
                if (b == (byte)'\r' || b == (byte)'\n' || b == (byte)' ' || b == (byte)'\t') {
                    if (seenNonWhitespace && !inWhitespace) {
                        destination[written++] = (byte)' ';
                        inWhitespace = true;
                    }
                }
                else {
                    destination[written++] = b;
                    inWhitespace = false;
                    seenNonWhitespace = true;
                }
            }

            if (written > 0 && destination[written - 1] == (byte)' ') {
                written--;
            }

            return written;
        }

        #endregion

        #region method TrimEndWsp

        /// <summary>
        /// Trims trailing ASCII space (<c>0x20</c>) and horizontal tab (<c>0x09</c>) bytes.
        /// </summary>
        /// <param name="span">The input byte span.</param>
        /// <returns>A slice of <paramref name="span"/> without trailing space or tab bytes.</returns>
        private static ReadOnlySpan<byte> TrimEndWsp(ReadOnlySpan<byte> span)
        {
            int end = span.Length;
            while (end > 0 && (span[end - 1] == (byte)' ' || span[end - 1] == (byte)'\t')) {
                end--;
            }

            return span.Slice(0, end);
        }

        #endregion

        #region method StripBTagValueBytes

        /// <summary>
        /// Removes the signature payload bytes from the <c>b=</c> tag in a raw <c>DKIM-Signature</c> header byte slice
        /// while preserving all surrounding bytes, whitespace, folding, and tags (RFC 6376 §3.7).
        /// </summary>
        /// <param name="rawDkimBytes">The raw bytes of the <c>DKIM-Signature</c> header value.</param>
        /// <returns>A copy of <paramref name="rawDkimBytes"/> with the <c>b=</c> value stripped.</returns>
        private static byte[] StripBTagValueBytes(ReadOnlySpan<byte> rawDkimBytes)
        {
            int idx = 0;
            while (idx < rawDkimBytes.Length) {
                int semiRel = rawDkimBytes.Slice(idx).IndexOf((byte)';');
                int segEnd = semiRel >= 0 ? idx + semiRel : rawDkimBytes.Length;

                ReadOnlySpan<byte> segment = rawDkimBytes.Slice(idx, segEnd - idx);
                int eqRel = segment.IndexOf((byte)'=');
                if (eqRel > 0) {
                    ReadOnlySpan<byte> tagSpan = segment.Slice(0, eqRel);
                    int start = 0;
                    while (start < tagSpan.Length && IsHeaderWspOrCrlf(tagSpan[start])) start++;
                    int end = tagSpan.Length;
                    while (end > start && IsHeaderWspOrCrlf(tagSpan[end - 1])) end--;

                    ReadOnlySpan<byte> trimmedTag = tagSpan.Slice(start, end - start);
                    if (trimmedTag.Length == 1 && (trimmedTag[0] == (byte)'b' || trimmedTag[0] == (byte)'B')) {
                        int eqAbs = idx + eqRel;
                        int prefixLen = eqAbs + 1;
                        int suffixLen = rawDkimBytes.Length - segEnd;
                        byte[] result = new byte[prefixLen + suffixLen];
                        rawDkimBytes.Slice(0, prefixLen).CopyTo(result);
                        rawDkimBytes.Slice(segEnd).CopyTo(result.AsSpan(prefixLen));
                        return result;
                    }
                }

                if (semiRel < 0)
                    break;

                idx = segEnd + 1;
            }

            return rawDkimBytes.ToArray();
        }

        #endregion

        #region method IsHeaderWspOrCrlf

        /// <summary>
        /// Determines whether the specified byte is ASCII whitespace or line-break (<c>0x20</c>, <c>0x09</c>, <c>0x0D</c>, or <c>0x0A</c>).
        /// </summary>
        /// <param name="b">The byte to test.</param>
        /// <returns><see langword="true"/> if <paramref name="b"/> is whitespace or CR/LF; otherwise <see langword="false"/>.</returns>
        private static bool IsHeaderWspOrCrlf(byte b) =>
            b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';

        #endregion

        #region method ParseCanonicalizationTag

        /// <summary>
        /// Parses the <c>c=</c> canonicalization tag (<c>header/body</c> or <c>header</c>) into boolean flags.
        /// </summary>
        /// <param name="cTagValue">The raw <c>c=</c> tag value, or <see langword="null"/> if omitted.</param>
        /// <returns>A tuple indicating whether header and body canonicalization are <c>relaxed</c> (<see langword="true"/>) or <c>simple</c> (<see langword="false"/>).</returns>
        private static (bool IsRelaxedHeader, bool IsRelaxedBody) ParseCanonicalizationTag(string? cTagValue)
        {
            if (string.IsNullOrWhiteSpace(cTagValue))
                return (false, false); // RFC 6376 §3.5: Default is simple/simple.

            string[] parts = cTagValue.Trim().ToLowerInvariant().Split('/', 2);
            bool headerRelaxed = parts[0].Trim() == "relaxed";
            bool bodyRelaxed = parts.Length > 1 && parts[1].Trim() == "relaxed";

            return (headerRelaxed, bodyRelaxed);
        }

        #endregion

        #region method ParseDkimTags

        /// <summary>
        /// Parses a semicolon-delimited DKIM tag-value list (RFC 6376 §3.2) into a case-insensitive dictionary.
        /// </summary>
        /// <param name="tagList">The unfolded tag-value string.</param>
        /// <returns>A dictionary mapping tag names to their raw values.</returns>
        private static Dictionary<string, string> ParseDkimTags(string tagList)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in tagList.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
                int eq = part.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();
                result[key] = value;
            }

            return result;
        }

        #endregion

        #region method ExtractPemFromDnsRecord

        /// <summary>
        /// Extracts the Base64 <c>p=</c> public key from a DNS TXT record string and formats it as a PEM SubjectPublicKeyInfo block.
        /// </summary>
        /// <param name="dnsRecordOrPem">Either a raw DKIM DNS TXT record (<c>"v=DKIM1; k=rsa; p=..."</c>) or an already-formatted PEM public key.</param>
        /// <returns>A PEM-formatted public key string, or <see cref="string.Empty"/> if the key is missing or revoked (<c>p=</c> empty).</returns>
        private static string ExtractPemFromDnsRecord(string dnsRecordOrPem)
        {
            string trimmed = dnsRecordOrPem.Trim();
            if (trimmed.StartsWith("-----BEGIN ", StringComparison.Ordinal))
                return trimmed;

            string base64Key;
            if (trimmed.Contains('=')) {
                Dictionary<string, string> tags = ParseDkimTags(trimmed);
                if (!tags.TryGetValue("p", out string? pVal) || string.IsNullOrWhiteSpace(pVal))
                    return string.Empty; // Empty p= indicates a revoked DKIM key per RFC 6376 §3.6.1.

                base64Key = RemoveAllWhitespace(pVal);
            }
            else {
                base64Key = RemoveAllWhitespace(trimmed);
            }

            if (string.IsNullOrEmpty(base64Key))
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN PUBLIC KEY-----");
            for (int i = 0; i < base64Key.Length; i += 64) {
                int len = Math.Min(64, base64Key.Length - i);
                sb.AppendLine(base64Key.Substring(i, len));
            }
            sb.Append("-----END PUBLIC KEY-----");

            return sb.ToString();
        }

        #endregion

        #region method ResolveDkimPublicKeyFromDnsAsync

        /// <summary>
        /// Queries DNS TXT records for a DKIM selector (<c>{selector}._domainkey.{domain}</c>) using <see cref="DNS_Client.Static"/>
        /// and extracts the <c>p=</c> public key value.
        /// </summary>
        /// <param name="domain">The signing domain from the <c>d=</c> tag.</param>
        /// <param name="selector">The selector from the <c>s=</c> tag.</param>
        /// <param name="cancellationToken">
        /// A token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// The value of the DNS DKIM public-key record, or
        /// <see langword="null"/> if no matching record was found.
        /// </returns>
        private static async Task<string?> ResolveDkimPublicKeyFromDnsAsync(string selector, string domain, CancellationToken cancellationToken)
        {
            string queryName = $"{selector}._domainkey.{domain}";
            DNS_Client dnsClient = DNS_Client.Static;
            var response = await dnsClient.QueryAsync(queryName, DNS_RecordType.TXT, 1000);
 
            if (response == null || response.ResponseCode != DNS_ResponseCode.NoError) {
               
                return null;
            }

            foreach (var txtRecord in response.Answers.TXT) {
                string rawTxt = txtRecord.Text ?? string.Empty;
                foreach (string part in rawTxt.Split(';')) {
                    string trimmed = part.Trim();
                    if (trimmed.StartsWith("p=", StringComparison.OrdinalIgnoreCase)) {
                        string pValue = trimmed.Substring(2).Trim();
                        // Per RFC 6376 §3.6.1, an empty p= means the key has been revoked.
                        if (pValue.Length > 0) {
                            return pValue;
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region method FoldBase64

        /// <summary>
        /// Folds a long Base64 signature string across lines with CRLF + leading space to stay within RFC 5322 line length limits.
        /// </summary>
        /// <param name="base64">The unfolded Base64 string.</param>
        /// <param name="lineWidth">Maximum characters per folded chunk.</param>
        /// <returns>The folded Base64 string.</returns>
        private static string FoldBase64(string base64, int lineWidth = 72)
        {
            if (base64.Length <= lineWidth)
                return base64;

            var sb = new StringBuilder(base64.Length + (base64.Length / lineWidth) * 3);
            for (int i = 0; i < base64.Length; i += lineWidth) {
                if (i > 0)
                    sb.Append("\r\n ");
                int len = Math.Min(lineWidth, base64.Length - i);
                sb.Append(base64, i, len);
            }

            return sb.ToString();
        }

        #endregion

        #region method RemoveAllWhitespace

        /// <summary>
        /// Strips all ASCII whitespace and line-break characters from a string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>A string with all whitespace characters removed.</returns>
        private static string RemoveAllWhitespace(string input)
        {
            var sb = new StringBuilder(input.Length);
            foreach (char ch in input) {
                if (!char.IsWhiteSpace(ch))
                    sb.Append(ch);
            }

            return sb.ToString();
        }

        #endregion
    }
}