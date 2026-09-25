using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using LumiSoft.Net.IO;
using LumiSoft.Net.Mail;

namespace LumiSoft.Net.POP3.Client
{
    /// <summary>
    /// Represents a single POP3 message within an active POP3 session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances of <see cref="POP3_ClientMessage"/> are created internally by
    /// <see cref="POP3_Client"/> when processing the POP3 <c>LIST</c> and
    /// <c>UIDL</c> commands. Each instance corresponds to a single message stored
    /// on the POP3 server and exposes convenience methods for retrieving message
    /// metadata, headers, partial content, full content, and for marking the
    /// message for deletion.
    /// </para>
    ///
    /// <para>
    /// POP3 message numbers are 1‑based and valid only for the duration of the
    /// current POP3 session, as defined in RFC 1939. The message size and unique
    /// identifier (UID) are obtained from the POP3 <c>LIST</c> and <c>UIDL</c>
    /// commands respectively. The UID is stable across sessions when supported
    /// by the server.
    /// </para>
    ///
    /// <para>
    /// This class provides both synchronous and asynchronous methods for:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Retrieving message headers using the POP3 <c>TOP</c> command.</description></item>
    ///   <item><description>Retrieving partial message content using a byte limit.</description></item>
    ///   <item><description>Retrieving full message content using the POP3 <c>RETR</c> command.</description></item>
    ///   <item><description>Parsing retrieved data into a <see cref="Mail_Message"/> MIME object.</description></item>
    ///   <item><description>Marking the message for deletion using the POP3 <c>DELE</c> command.</description></item>
    /// </list>
    ///
    /// <para>
    /// Header‑only and partial‑body retrieval methods are useful for lightweight
    /// message inspection, such as reading <c>Subject</c>, <c>From</c>, <c>Date</c>,
    /// or other metadata without downloading the full message body. The MIME parser
    /// used by <see cref="Mail_Message.ParseFromStream(Stream)"/> supports partial
    /// MIME structures, allowing safe parsing of truncated message data.
    /// </para>
    ///
    /// <para>
    /// A message marked for deletion remains accessible for the duration of the
    /// session. The POP3 server performs the actual deletion only when the session
    /// ends with <c>QUIT</c>, entering the UPDATE state as defined in RFC 1939.
    /// </para>
    ///
    /// <para>
    /// All synchronous methods in this class internally wrap their asynchronous
    /// counterparts and use the POP3 client's configured timeout to ensure
    /// consistent blocking behavior across the API.
    /// </para>
    ///
    /// <para>
    /// Instances of this class are not thread‑safe. Concurrent access should be
    /// coordinated externally if required.
    /// </para>
    /// </remarks>
    public class POP3_ClientMessage
    {
        private POP3_Client m_Pop3Client;
        private int         m_MessageNumber       = 1;
        private string?     m_UniqueId            = null;
        private int         m_Size                = 0;
        private bool        m_IsMarkedForDeletion = false;
        private bool        m_IsDisposed          = false;

        /// <summary>
        /// Initializes a new POP3 client message instance using values obtained
        /// from the POP3 <c>LIST</c> and <c>UIDL</c> commands.
        /// </summary>
        /// <param name="pop3">
        /// The owning <see cref="POP3_Client"/> instance that manages the POP3 session.
        /// </param>
        /// <param name="messageNumber">
        /// The 1‑based POP3 message number as returned by the <c>LIST</c> command.
        /// Message numbers are valid only for the duration of the current session.
        /// </param>
        /// <param name="size">
        /// The message size in bytes as reported by the POP3 <c>LIST</c> command.
        /// </param>
        /// <param name="uniqueId">
        /// The POP3 unique identifier (UID) returned by the <c>UIDL</c> command,
        /// or <c>null</c> if the server does not support UIDL.
        /// </param>
        /// <remarks>
        /// This constructor is used internally when building the message collection
        /// after issuing POP3 <c>LIST</c> and optionally <c>UIDL</c>. The values
        /// assigned here remain unchanged for the lifetime of the message object.
        /// </remarks>
        internal POP3_ClientMessage(POP3_Client pop3,int messageNumber,int size,string? uniqueId)
        {
            m_Pop3Client     = pop3;
            m_MessageNumber  = messageNumber;
            m_Size           = size;
            m_UniqueId       = uniqueId;
        }


        #region method MarkForDeletion

        /// <summary>
        /// Marks the message for deletion using the POP3 <c>DELE</c> command.
        /// This is the synchronous wrapper for
        /// <see cref="MarkForDeletionAsync(CancellationToken)"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is already marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks the calling thread until the underlying asynchronous
        /// <see cref="MarkForDeletionAsync(CancellationToken)"/> operation completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the same
        /// timeout behavior as other synchronous POP3 operations.
        /// </para>
        /// <para>
        /// The POP3 <c>DELE</c> command marks the message for deletion during the
        /// current session. The server performs the actual deletion only when the
        /// session ends with <c>QUIT</c>, entering the UPDATE state as defined in
        /// RFC 1939.
        /// </para>
        /// </remarks>
        public void MarkForDeletion()
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            MarkForDeletionAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MarkForDeletionAsync

        /// <summary>
        /// Asynchronously marks the message for deletion using the POP3 <c>DELE</c>
        /// command. The message is not physically removed until the POP3 session
        /// enters the UPDATE state (after <c>QUIT</c>), as defined in RFC 1939.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous POP3 <c>DELE</c>
        /// operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is already marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method sends the POP3 <c>DELE</c> command for the current message
        /// number. A successful <c>+OK</c> response marks the message as deleted
        /// for the remainder of the session.
        /// </para>
        /// <para>
        /// The actual deletion occurs only when the POP3 session ends with
        /// <c>QUIT</c>, at which point the server enters the UPDATE state.
        /// </para>
        /// </remarks>
        public async ValueTask MarkForDeletionAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            if(m_IsMarkedForDeletion){
                return;
            }

            await m_Pop3Client.DeleAsync(this.MessageNumber,cancellationToken);
            m_IsMarkedForDeletion = true;
        }

        #endregion

        #region method HeaderToMailMessage

        /// <summary>
        /// Retrieves the message header from the POP3 server and parses it into a
        /// <see cref="Mail_Message"/> instance.
        /// This is the synchronous wrapper for <see cref="HeaderToMailMessageAsync(CancellationToken)"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="Mail_Message"/> containing only the parsed header fields.
        /// The message body is not retrieved or parsed.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper blocks the calling thread until the underlying
        /// asynchronous <see cref="HeaderToMailMessageAsync(CancellationToken)"/>
        /// operation completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the same
        /// timeout behavior as other synchronous POP3 operations.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// Header‑only parsing is useful for lightweight message inspection, such as
        /// reading <c>Subject</c>, <c>From</c>, <c>Date</c>, or other metadata without
        /// downloading the full message body.
        /// </para>
        /// <para>
        /// If the retrieved header data is truncated before the terminating
        /// <c>CRLFCRLF</c>, the MIME parser may interpret the header block as incomplete.
        /// </para>
        /// </remarks>
        public Mail_Message HeaderToMailMessage()
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            return HeaderToMailMessageAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToMailMessageAsync

        /// <summary>
        /// Retrieves the message header using the POP3 <c>TOP</c> command with a line
        /// count of <c>0</c> and returns the result as a UTF‑8 string.
        /// This is the synchronous wrapper for <see cref="HeaderToStringAsync(CancellationToken)"/>.
        /// </summary>
        /// <returns>
        /// A UTF‑8 decoded string containing the POP3 header data.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks the calling thread until the underlying asynchronous
        /// <see cref="HeaderToStringAsync(CancellationToken)"/> operation completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the same
        /// timeout behavior as other synchronous POP3 operations.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// The POP3 <c>TOP</c> command with a line count of <c>0</c> returns only the
        /// message header followed by a blank line, as defined in RFC 1939.
        /// </para>
        /// </remarks>
        public async ValueTask<Mail_Message> HeaderToMailMessageAsync(CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStreamEx();

            await HeaderToStreamAsync(stream,cancellationToken);
            stream.Position = 0;

            return Mail_Message.ParseFromStream(stream);
        }

        #endregion

        #region method HeaderToString

        /// <summary>
        /// Retrieves the message header using the POP3 <c>TOP</c> command with a line
        /// count of <c>0</c> and returns the result as a UTF‑8 string.
        /// This is the synchronous wrapper for <see cref="HeaderToStringAsync(CancellationToken)"/>.
        /// </summary>
        /// <returns>
        /// A UTF‑8 decoded string containing the POP3 header data.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks the calling thread until the underlying asynchronous
        /// <see cref="HeaderToStringAsync(CancellationToken)"/> operation completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the same
        /// timeout behavior as other synchronous POP3 operations.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// The POP3 <c>TOP</c> command with a line count of <c>0</c> returns only the
        /// message header followed by a blank line, as defined in RFC 1939.
        /// </para>
        /// </remarks>
        public string HeaderToString()
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            return HeaderToStringAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToStringAsync

        /// <summary>
        /// Asynchronously retrieves the message header using the POP3 <c>TOP</c> command
        /// with a line count of <c>0</c> and returns the result as a UTF‑8 string.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> whose result contains the POP3 header data
        /// decoded as a UTF‑8 string.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a convenience wrapper around <see cref="HeaderToByteAsync"/>
        /// that retrieves the header as raw bytes and decodes the result using UTF‑8.
        /// </para>
        /// <para>
        /// The POP3 <c>TOP</c> command with a line count of <c>0</c> returns only the
        /// message header followed by a blank line, as defined in RFC 1939.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// For scenarios where memory usage is a concern or when streaming is preferred,
        /// use <see cref="HeaderToStreamAsync"/> instead.
        /// </para>
        /// </remarks>
        public async ValueTask<string> HeaderToStringAsync(CancellationToken cancellationToken = default)
        {
            byte[] header = await HeaderToByteAsync(cancellationToken);
            
            return Encoding.UTF8.GetString(header);
        }

        #endregion

        #region method HeaderToByte

        /// <summary>
        /// Retrieves the message header using the POP3 <c>TOP</c> command with a line
        /// count of <c>0</c> and returns the result as a byte array.
        /// This is the synchronous wrapper for <see cref="HeaderToByteAsync(CancellationToken)"/>.
        /// </summary>
        /// <returns>
        /// A byte array containing the POP3 header data.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks the calling thread until the underlying asynchronous
        /// <see cref="HeaderToByteAsync(CancellationToken)"/> operation completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the same
        /// timeout behavior as other synchronous POP3 operations.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// The POP3 <c>TOP</c> command with a line count of <c>0</c> returns only the
        /// message header followed by a blank line, as defined in RFC 1939.
        /// </para>
        /// </remarks>
        public byte[] HeaderToByte()
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            return HeaderToByteAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToByteAsync

        /// <summary>
        /// Asynchronously retrieves the message header using the POP3 <c>TOP</c> command
        /// with a line count of <c>0</c> and returns the result as a byte array.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> whose result contains the POP3 header data
        /// as a byte array.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a convenience wrapper around <see cref="HeaderToStreamAsync"/>
        /// that buffers the streamed header data into an in‑memory <see cref="MemoryStream"/>
        /// and returns the resulting byte array.
        /// </para>
        /// <para>
        /// The POP3 <c>TOP</c> command with a line count of <c>0</c> returns only the
        /// message header followed by a blank line, as defined in RFC 1939.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// For large messages or when memory usage is a concern, prefer
        /// <see cref="HeaderToStreamAsync"/> or <see cref="HeaderToStream"/> to stream
        /// the header directly to a file or processing pipeline.
        /// </para>
        /// </remarks>
        public async ValueTask<byte[]> HeaderToByteAsync(CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();

            await HeaderToStreamAsync(stream,cancellationToken);

            return stream.ToArray();
        }

        #endregion

        #region method HeaderToStream

        /// <summary>
        /// Retrieves the message header using the POP3 <c>TOP</c> command with a line
        /// count of <c>0</c> and writes the result to the specified stream.
        /// This is the synchronous wrapper for <see cref="HeaderToStreamAsync"/>.
        /// </summary>
        /// <param name="stream">
        /// The destination <see cref="Stream"/> to which the POP3 header data will be written.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks the calling thread until the underlying asynchronous
        /// <see cref="HeaderToStreamAsync"/> operation completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the same
        /// timeout behavior as other synchronous POP3 operations.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// The POP3 <c>TOP</c> command with a line count of <c>0</c> returns only the
        /// message header followed by a blank line, as defined in RFC 1939.
        /// </para>
        /// </remarks>
        public void HeaderToStream(Stream stream)
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            HeaderToStreamAsync(stream,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToStreamAsync

        /// <summary>
        /// Asynchronously retrieves the message header using the POP3 <c>TOP</c> command
        /// with a line count of <c>0</c> and writes the result to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// The destination <see cref="Stream"/> to which the POP3 header data will be written.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous POP3 <c>TOP</c> operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the message is marked for deletion.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a convenience wrapper around <see cref="TopLinesToStreamAsync"/>
        /// that requests <c>0</c> body lines, causing the POP3 server to return only the
        /// message header followed by a blank line, as defined by RFC 1939.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// The header data is streamed directly to the provided <paramref name="stream"/>
        /// without caching or parsing.
        /// </para>
        /// </remarks>
        public async ValueTask HeaderToStreamAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            await TopLinesToStreamAsync(0,stream,cancellationToken);
        }

        #endregion

        #region method ToMailMessage

        /// <summary>
        /// Retrieves a portion of the message using the POP3 <c>RETR</c> command and
        /// parses the retrieved data into a <see cref="Mail_Message"/> instance.  
        /// This is the synchronous wrapper for <see cref="ToMailMessageAsync(CancellationToken)"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="Mail_Message"/> representing the parsed message content.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper blocks the calling thread until the underlying
        /// asynchronous <see cref="ToMailMessageAsync(CancellationToken)"/> operation
        /// completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the same
        /// operation timeout as other synchronous POP3 methods.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// If the retrieved data does not include the complete header section
        /// (terminated by <c>CRLFCRLF</c>), the MIME parser may interpret the header
        /// block as incomplete.
        /// </para>
        /// <para>
        /// Partial body retrieval is useful for header inspection or lightweight
        /// message analysis without downloading the full message body.
        /// </para>
        /// </remarks>
        public Mail_Message ToMailMessage()
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            return ToMailMessageAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToMailMessageAsync

        /// <summary>
        /// Retrieves a portion of the message using the POP3 <c>RETR</c> command and
        /// parses the retrieved data into a <see cref="Mail_Message"/> instance.
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="Mail_Message"/> representing the parsed message content.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method performs a partial POP3 <c>RETR</c> operation by retrieving only
        /// the portion of the message allowed by the internal read limit enforced by
        /// <see cref="ToStreamAsync(Stream, CancellationToken)"/>.  
        /// The limit is based on the message size reported by the POP3 <c>LIST</c>
        /// command, plus an additional one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// If the retrieved data does not include the complete header section
        /// (terminated by <c>CRLFCRLF</c>), the MIME parser may interpret the header
        /// block as incomplete.
        /// </para>
        /// <para>
        /// Partial body retrieval is useful for header inspection or lightweight
        /// message analysis without downloading the full message body.
        /// </para>
        /// </remarks>
        public async ValueTask<Mail_Message> ToMailMessageAsync(CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStreamEx();

            await ToStreamAsync(stream,cancellationToken);
            stream.Position = 0;

            return Mail_Message.ParseFromStream(stream);
        }

        #endregion

        #region method ToByte

        /// <summary>
        /// Retrieves the full message using the POP3 <c>RETR</c> command and returns
        /// the result as a byte array.  
        /// This is the synchronous wrapper for <see cref="ToByteAsync(CancellationToken)"/>.
        /// </summary>
        /// <returns>
        /// A byte array containing the full POP3 <c>RETR</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks the calling thread until the underlying asynchronous
        /// <see cref="ToByteAsync(CancellationToken)"/> operation completes.
        /// </para>
        /// <para>
        /// A <see cref="CancellationTokenSource"/> is created using the POP3 client's
        /// configured timeout, ensuring that the synchronous wrapper respects the
        /// same operation timeout as other synchronous POP3 methods.
        /// </para>
        /// <para>
        /// <b>Warning:</b> This method buffers the entire POP3 <c>RETR</c> response
        /// into memory.  
        /// For large messages, this may result in significant memory usage.  
        /// When working with large messages or when memory usage is a concern,
        /// prefer <see cref="ToStream"/> or <see cref="ToStreamAsync"/> to stream the
        /// message directly to a file or processing pipeline.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// </remarks>
        public byte[] ToByte()
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            return ToByteAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToByteAsync

        /// <summary>
        /// Retrieves the full message using the POP3 <c>RETR</c> command and returns
        /// the result as a byte array.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> whose result contains the full POP3
        /// <c>RETR</c> response as a byte array.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the internal stream allocation fails.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a convenience wrapper around <see cref="ToStreamAsync"/> that
        /// buffers the entire POP3 <c>RETR</c> response into an in‑memory
        /// <see cref="MemoryStream"/> and returns the resulting byte array.
        /// </para>
        /// <para>
        /// <b>Warning:</b> For large messages, using this method may result in
        /// significant memory usage because the entire message is stored in RAM.  
        /// When working with large messages or when memory usage is a concern,
        /// prefer <see cref="ToStreamAsync"/> or <see cref="ToStream"/> to stream the
        /// message directly to a file or processing pipeline.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// </remarks>
        public async ValueTask<byte[]> ToByteAsync(CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();

            await ToStreamAsync(stream,cancellationToken);

            return stream.ToArray();
        }

        #endregion

        #region method ToStream

        /// <summary>
        /// Retrieves the full message using the POP3 <c>RETR</c> command and writes the
        /// result to the provided stream.  
        /// This is the synchronous wrapper for <see cref="ToStreamAsync"/> and uses the
        /// underlying TCP client's read/write timeout to limit how long the operation
        /// may block.
        /// </summary>
        /// <param name="stream">
        /// The destination <see cref="Stream"/> to which the POP3 <c>RETR</c> response
        /// will be written.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>RETR</c> command returns the entire message, including all
        /// headers and the full body.  
        /// The response is transmitted using POP3 multi‑line format and terminated by a
        /// single period (<c>.</c>) on a line by itself.
        /// </para>
        /// <para>
        /// This synchronous wrapper blocks the calling thread until the operation
        /// completes.  
        /// The timeout applied to this operation is inherited from the underlying
        /// TCP client and controls how long the wrapper waits for network activity.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// For non‑blocking usage, call <see cref="ToStreamAsync"/> instead.
        /// </para>
        /// </remarks>
        public void ToStream(Stream stream)
        {
            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            ToStreamAsync(stream,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToStreamAsync

        /// <summary>
        /// Retrieves the full message using the POP3 <c>RETR</c> command and writes the
        /// result to the provided stream.
        /// </summary>
        /// <param name="stream">
        /// The destination <see cref="Stream"/> to which the POP3 <c>RETR</c> response
        /// will be written.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous POP3 <c>RETR</c>
        /// operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>RETR</c> command returns the entire message, including all
        /// headers and the full body.  
        /// The response is transmitted using POP3 multi‑line format and terminated by a
        /// single period (<c>.</c>) on a line by itself.
        /// </para>
        /// <para>
        /// The method enforces an internal read limit based on the message size
        /// reported by the POP3 <c>LIST</c> command, plus an additional one‑megabyte
        /// safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// This method does not cache or parse the returned data; it streams the raw
        /// POP3 response directly into the provided <paramref name="stream"/>.
        /// </para>
        /// </remarks>
        public async ValueTask ToStreamAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            int maxCount = m_Size + (1000*1000);

            await m_Pop3Client.RetrAsync(this.MessageNumber,stream,maxCount,cancellationToken);
        }

        #endregion

        #region method TopLinesToByte

        /// <summary>
        /// Retrieves the message headers and the specified number of body lines using
        /// the POP3 <c>TOP</c> command and returns the result as a byte array.
        /// This is the synchronous wrapper for <see cref="TopLinesToByteAsync"/>.
        /// </summary>
        /// <param name="lineCount">
        /// The number of body lines to retrieve.
        /// A value of <c>0</c> retrieves only the message header.
        /// </param>
        /// <returns>
        /// A byte array containing the POP3 <c>TOP</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="lineCount"/> is negative.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper blocks the calling thread until the underlying
        /// asynchronous operation completes.
        /// </para>
        /// <para>
        /// <b>Warning:</b> This method buffers the entire POP3 <c>TOP</c> response into
        /// memory.  
        /// For large messages, this may result in significant memory usage.  
        /// When working with large messages, prefer
        /// <see cref="TopLinesToStream"/> or <see cref="TopLinesToStreamAsync"/> to
        /// stream the data directly to a file or processing pipeline.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// </remarks>
        public byte[] TopLinesToByte(int lineCount)
        {
            return TopLinesToByteAsync(lineCount).GetAwaiter().GetResult();
        }

        #endregion

        #region method TopLinesToByteAsync

        /// <summary>
        /// Retrieves the message headers and the specified number of body lines using
        /// the POP3 <c>TOP</c> command and returns the result as a byte array.
        /// </summary>
        /// <param name="lineCount">
        /// The number of body lines to retrieve.
        /// A value of <c>0</c> retrieves only the message header.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> whose result contains the POP3 <c>TOP</c>
        /// response as a byte array.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="lineCount"/> is negative.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a convenience wrapper around
        /// <see cref="TopLinesToStreamAsync"/> that captures the POP3 <c>TOP</c>
        /// response into an in‑memory <see cref="MemoryStream"/> and returns the
        /// resulting byte array.
        /// </para>
        /// <para>
        /// The POP3 <c>TOP</c> command returns the message header followed by the first
        /// <paramref name="lineCount"/> lines of the message body.
        /// The response is transmitted using POP3 multi‑line format and terminated by a
        /// single period (<c>.</c>) on a line by itself.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// </remarks>
        public async ValueTask<byte[]> TopLinesToByteAsync(int lineCount,CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();

            await TopLinesToStreamAsync(lineCount,stream,cancellationToken);

            return stream.ToArray();
        }

        #endregion

        #region method TopLinesToStream

        /// <summary>
        /// Retrieves the message headers and the specified number of body lines using
        /// the POP3 <c>TOP</c> command and writes the result to the provided stream.
        /// This is the synchronous wrapper for <see cref="TopLinesToStreamAsync"/> and
        /// uses the underlying TCP client's read/write timeout to limit the duration
        /// of the operation.
        /// </summary>
        /// <param name="lineCount">
        /// The number of body lines to retrieve.
        /// A value of <c>0</c> retrieves only the message header.
        /// </param>
        /// <param name="stream">
        /// The destination <see cref="Stream"/> to which the POP3 <c>TOP</c> response
        /// will be written.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="lineCount"/> is negative.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown if the POP3 server returns a negative (<c>-ERR</c>) response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>TOP</c> command returns the message header followed by the first
        /// <paramref name="lineCount"/> lines of the message body.
        /// The response is transmitted using POP3 multi‑line format and terminated by a
        /// single period (<c>.</c>) on a line by itself.
        /// </para>
        /// <para>
        /// This synchronous wrapper blocks the calling thread until the operation
        /// completes.  
        /// The timeout applied to this operation is inherited from the underlying
        /// TCP client and controls how long the wrapper waits for network activity.
        /// </para>
        /// <para>
        /// The underlying asynchronous method enforces an internal read limit based on
        /// the message size reported by the POP3 <c>LIST</c> command, plus an additional
        /// one‑megabyte safety margin.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// For non‑blocking usage, call <see cref="TopLinesToStreamAsync"/> instead.
        /// </para>
        /// </remarks>
        public void TopLinesToStream(int lineCount,Stream stream)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            using var cts = new CancellationTokenSource(m_Pop3Client.Timeout);

            TopLinesToStreamAsync(lineCount,stream,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method TopLinesToStreamAsync

        /// <summary>
        /// Retrieves the message headers and the specified number of body lines using
        /// the POP3 <c>TOP</c> command and writes the result to the provided stream.
        /// </summary>
        /// <param name="lineCount">
        /// The number of body lines to retrieve.
        /// A value of <c>0</c> retrieves only the message header.
        /// </param>
        /// <param name="stream">
        /// The destination <see cref="Stream"/> to which the POP3 <c>TOP</c> response
        /// will be written.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous POP3 <c>TOP</c>
        /// operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="lineCount"/> is negative.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>TOP</c> command returns the message header followed by the first
        /// <paramref name="lineCount"/> lines of the message body.  
        /// The response is transmitted using POP3 multi‑line format and terminated by a
        /// single period (<c>.</c>) on a line by itself.
        /// </para>
        /// <para>
        /// The method enforces an internal read limit based on the message size
        /// reported by the POP3 <c>LIST</c> command, plus an additional safety margin
        /// of one megabyte.  
        /// This limit is applied automatically and is not exposed to the caller.
        /// </para>
        /// <para>
        /// This method does not cache or parse the returned data; it simply streams the
        /// raw POP3 response into the provided <paramref name="stream"/>.
        /// </para>
        /// </remarks>
        public async ValueTask TopLinesToStreamAsync(int lineCount,Stream stream,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(lineCount < 0){
                throw new ArgumentException("Line count must be >= 0.",nameof(lineCount));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            int maxCount = m_Size + (1000*1000);

            await m_Pop3Client.TopAsync(this.MessageNumber,lineCount,stream,maxCount,cancellationToken);
        }

        #endregion


        #region method Dispose

        /// <summary>
        /// Disposes message.
        /// </summary>
        internal void Dispose()
        {
            if(m_IsDisposed){
                return;
            }

            m_IsDisposed = true;
        }

        #endregion


        #region method SetMarkedForDeletion

        /// <summary>
        /// Sets IsMarkedForDeletion flag value.
        /// </summary>
        /// <param name="isMarkedForDeletion">New IsMarkedForDeletion value.</param>
        internal void SetMarkedForDeletion(bool isMarkedForDeletion)
        {
            m_IsMarkedForDeletion = isMarkedForDeletion;
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets a value indicating whether this message object has been disposed.
        /// Once disposed, no further POP3 operations may be performed on the instance.
        /// </summary>
        /// <remarks>
        /// Accessing any property or invoking any method on a disposed message object
        /// results in an <see cref="ObjectDisposedException"/>. Disposal affects only
        /// the client-side representation of the message and does not modify the
        /// message state on the POP3 server.
        /// </remarks>
        public bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }

        /// <summary>
        /// Gets the POP3 message number associated with this message.
        /// Message numbers are 1‑based and correspond to the server‑assigned
        /// sequence numbers returned by POP3 <c>LIST</c>, <c>UIDL</c>, and
        /// other message‑indexed commands.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// POP3 message numbers are stable only for the duration of the current
        /// session. After a successful <c>DELE</c> command, the message number
        /// remains visible in <c>LIST</c> and <c>UIDL</c> output until the session
        /// ends with <c>QUIT</c>, at which point the server enters the UPDATE
        /// state and applies pending deletions.
        /// </para>
        /// </remarks>
        public int MessageNumber
        {
            get{               
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_MessageNumber; 
            }
        }

        /// <summary>
        /// Gets the POP3 unique identifier (UID) associated with this message.
        /// The UID is the server‑assigned value returned by the POP3 <c>UIDL</c>
        /// command and is intended to be stable across sessions.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// POP3 unique identifiers allow clients to track messages between
        /// sessions and detect which messages have already been downloaded.
        /// Unlike message numbers, which are 1‑based and may change between
        /// sessions, UIDs are designed to remain constant for the lifetime of
        /// the message on the server.
        /// </para>
        /// <para>
        /// If the server does not support <c>UIDL</c>, this value may be
        /// <c>null</c>.
        /// </para>
        /// </remarks>
        public string? UniqueId
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_UniqueId; 
            }
        }

        /// <summary>
        /// Gets the size of the message in bytes as reported by the POP3 <c>LIST</c>
        /// command. The value represents the octet count of the message on the server.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The size value is obtained from the POP3 <c>LIST</c> response and reflects
        /// the message's storage size on the server. It does not necessarily correspond
        /// to the exact number of bytes returned by <c>RETR</c>, since line termination
        /// normalization (<c>CRLF</c>) may affect the final byte count.
        /// </para>
        /// <para>
        /// The reported size remains visible even after a successful <c>DELE</c>
        /// command, until the session ends with <c>QUIT</c> and the server enters the
        /// UPDATE state.
        /// </para>
        /// </remarks>
        public int Size
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_Size;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the message has been marked for deletion
        /// during the current POP3 session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message object has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A message becomes marked for deletion after a successful POP3 <c>DELE</c>
        /// command. While marked, any further reference to its message number in
        /// commands such as <c>RETR</c>, <c>TOP</c>, or <c>DELE</c> will result in a
        /// negative (<c>-ERR</c>) server response.
        /// </para>
        /// <para>
        /// The message continues to appear in <c>LIST</c> and <c>UIDL</c> output until
        /// the session ends with <c>QUIT</c>, at which point the server enters the
        /// UPDATE state and applies pending deletions.
        /// </para>
        /// </remarks>
        public bool IsMarkedForDeletion
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_IsMarkedForDeletion; 
            }
        }

        #endregion

    }
}
