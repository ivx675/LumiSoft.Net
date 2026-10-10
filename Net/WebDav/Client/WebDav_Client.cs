using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace LumiSoft.Net.WebDav.Client
{
    /// <summary>
    /// Implements WebDav client. Defined in RFC 4918.
    /// </summary>
    public class WebDav_Client
    {
        private          NetworkCredential? m_pCredentials = null;
        private readonly HttpClientHandler  m_pHttpClientHandler;
        private readonly HttpClient         m_pHttpClient;

        /// <summary>
        /// Default constructor. Initializes a new instance of the <see cref="WebDav_Client"/> class.
        /// </summary>
        public WebDav_Client()
        {
            m_pHttpClientHandler = new HttpClientHandler();
            
            // Set a default timeout of 60 seconds (non-blocking)
            m_pHttpClient = new HttpClient(m_pHttpClientHandler){
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebDav_Client"/> class with a custom <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="httpClient">The pre-configured <see cref="HttpClient"/> instance to use for requests.</param>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="httpClient"/> is a null reference.</exception>
        public WebDav_Client(HttpClient httpClient)
        {
            m_pHttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            m_pHttpClientHandler = null!; // Handled externally if custom HttpClient is passed
        }

        #region method PropFind

        /// <summary>
        /// Executes PROPFIND method asynchronously.
        /// </summary>
        /// <param name="requestUri">The target request URI.</param>
        /// <param name="propertyNames">Properties to get. A value of null or empty means property names listing.</param>
        /// <param name="depth">Maximum depth inside collections to get (-1 for unspecified).</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns server returned <see cref="WebDav_MultiStatus"/> responses.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="requestUri"/> is a null reference.</exception>
        public async Task<WebDav_MultiStatus> PropFindAsync(string requestUri, string[]? propertyNames, int depth, CancellationToken cancellationToken = default)
        {
            if(requestUri == null){
                throw new ArgumentNullException(nameof(requestUri));
            }

            StringBuilder requestContentString = new StringBuilder();
            requestContentString.Append("<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n");
            requestContentString.Append("<propfind xmlns=\"DAV:\">\r\n");
            requestContentString.Append("<prop>\r\n");
            if(propertyNames == null || propertyNames.Length == 0){
                requestContentString.Append("   <propname/>\r\n");
            }
            else{
                foreach(string propertyName in propertyNames){
                    requestContentString.Append("<" + propertyName + "/>");
                }
            }
            requestContentString.Append("</prop>\r\n");
            requestContentString.Append("</propfind>\r\n");

            var content = new StringContent(requestContentString.ToString(), Encoding.UTF8, "application/xml");

            using var requestMessage = new HttpRequestMessage(new HttpMethod("PROPFIND"), requestUri)
            {
                Content = content
            };

            if(depth > -1){
                requestMessage.Headers.Add("Depth", depth.ToString());
            }

            using var response = await m_pHttpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            return WebDav_MultiStatus.Parse(responseStream);
        }

        #endregion

        #region method MkCol

        /// <summary>
        /// Creates new collection to the specified path asynchronously.
        /// </summary>
        /// <param name="uri">Target collection URI.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="uri"/> is a null reference.</exception>
        public async Task MkColAsync(string uri, CancellationToken cancellationToken = default)
        {
            if(uri == null){
                throw new ArgumentNullException(nameof(uri));
            }

            using var requestMessage = new HttpRequestMessage(new HttpMethod("MKCOL"), uri);
            using var response = await m_pHttpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region method Get
        
        /// <summary>
        /// Gets the specified resource stream asynchronously.
        /// </summary>
        /// <param name="uri">Target resource URI.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns a tuple containing the resource stream and its content size in bytes (if available).</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="uri"/> is a null reference.</exception>
        public async Task<(Stream Stream, long? ContentSize)> GetAsync(string uri, CancellationToken cancellationToken = default)
        {
            if(uri == null){
                throw new ArgumentNullException(nameof(uri));
            }

            var response = await m_pHttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long? contentSize = response.Content.Headers.ContentLength;
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            return (stream, contentSize);
        }

        #endregion

        #region method Delete

        /// <summary>
        /// Deletes specified resource asynchronously.
        /// </summary>
        /// <param name="uri">Target URI. For example: http://server/test.txt .</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="uri"/> is a null reference.</exception>
        public async Task DeleteAsync(string uri, CancellationToken cancellationToken = default)
        {
            if(uri == null){
                throw new ArgumentNullException(nameof(uri));
            }

            using var response = await m_pHttpClient.DeleteAsync(uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region method Put

        /// <summary>
        /// Creates specified resource to the specified location asynchronously.
        /// </summary>
        /// <param name="targetUri">Target URI. For example: http://server/test.txt .</param>
        /// <param name="stream">Stream containing data to upload.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="targetUri"/> or <paramref name="stream"/> is a null reference.</exception>
        public async Task PutAsync(string targetUri, Stream stream, CancellationToken cancellationToken = default)
        {
            if(targetUri == null){
                throw new ArgumentNullException(nameof(targetUri));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Put, targetUri)
            {
                Content = content
            };

            using var response = await m_pHttpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region method Copy

        /// <summary>
        /// Copies source URI resource to the target URI asynchronously.
        /// </summary>
        /// <param name="sourceUri">Source URI.</param>
        /// <param name="targetUri">Target URI.</param>
        /// <param name="depth">If source is collection, then depth specifies how many nested levels will be copied (-1 for unspecified).</param>
        /// <param name="overwrite">If true and target resource already exists, it will be overwritten. If false and target resource exists, an exception is thrown.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="sourceUri"/> or <paramref name="targetUri"/> is a null reference.</exception>
        public async Task CopyAsync(string sourceUri, string targetUri, int depth, bool overwrite, CancellationToken cancellationToken = default)
        {
            if(sourceUri == null){
                throw new ArgumentNullException(nameof(sourceUri));
            }
            if(targetUri == null){
                throw new ArgumentNullException(nameof(targetUri));
            }

            using var requestMessage = new HttpRequestMessage(new HttpMethod("COPY"), sourceUri);
            requestMessage.Headers.Add("Destination", targetUri);
            requestMessage.Headers.Add("Overwrite", overwrite ? "T" : "F");
            if (depth > -1)
            {
                requestMessage.Headers.Add("Depth", depth.ToString());
            }

            using var response = await m_pHttpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region method Move

        /// <summary>
        /// Moves source URI resource to the target URI asynchronously.
        /// enforce handling.
        /// </summary>
        /// <param name="sourceUri">Source URI.</param>
        /// <param name="targetUri">Target URI.</param>
        /// <param name="depth">If source is collection, then depth specifies how many nested levels will be moved (-1 for unspecified).</param>
        /// <param name="overwrite">If true and target resource already exists, it will be overwritten. If false and target resource exists, an exception is thrown.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="sourceUri"/> or <paramref name="targetUri"/> is a null reference.</exception>
        public async Task MoveAsync(string sourceUri, string targetUri, int depth, bool overwrite, CancellationToken cancellationToken = default)
        {
            if(sourceUri == null){
                throw new ArgumentNullException(nameof(sourceUri));
            }
            if(targetUri == null){
                throw new ArgumentNullException(nameof(targetUri));
            }

            using var requestMessage = new HttpRequestMessage(new HttpMethod("MOVE"), sourceUri);
            requestMessage.Headers.Add("Destination", targetUri);
            requestMessage.Headers.Add("Overwrite", overwrite ? "T" : "F");
            if(depth > -1){
                requestMessage.Headers.Add("Depth", depth.ToString());
            }

            using var response = await m_pHttpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        #endregion

        #region Properties implementation

        /// <summary>
        /// Gets or sets credentials for network requests.
        /// </summary>
        public NetworkCredential? Credentials
        {
            get => m_pCredentials;

            set{
                m_pCredentials = value;
                if(m_pHttpClientHandler != null){
                    m_pHttpClientHandler.Credentials = m_pCredentials;
                }
            }
        }

        #endregion
    }
}