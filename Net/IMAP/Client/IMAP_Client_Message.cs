using LumiSoft.Net.IO;
using LumiSoft.Net.Mail;
using LumiSoft.Net.POP3.Client;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LumiSoft.Net.IMAP.Client 
{
    /// <summary>
    /// Represents a single IMAP message within a selected mailbox. Instances of
    /// <see cref="IMAP_Client_Message"/> are created internally by
    /// <see cref="IMAP_Client_MessageSet"/> from the results of a bulk
    /// <c>FETCH</c> operation and expose both prefetched and lazily retrieved
    /// message metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A message object provides access to common IMAP metadata such as UID,
    /// size, internal date, flags, envelope, body structure, and (when enabled)
    /// Gmail‑specific extensions. Metadata that was not prefetched during
    /// message‑set creation remains <c>null</c> until explicitly retrieved using
    /// <see cref="GetEnvelopeAsync"/> or <see cref="GetBodyStructureAsync"/>.
    /// </para>
    /// <para>
    /// In addition to metadata access, the class provides convenience methods
    /// for common per‑message IMAP operations such as copying, moving, flag
    /// manipulation, and retrieving the RFC 5322 header block.
    /// </para>
    /// <para>
    /// The object does not perform any IMAP communication during construction.
    /// All prefetched values originate from the <c>FETCH</c> response that
    /// created the containing <see cref="IMAP_Client_MessageSet"/>. Lazy
    /// retrieval methods perform targeted <c>FETCH UID</c> requests only when
    /// needed, and cache the results for subsequent access.
    /// </para>
    /// <para>
    /// A message instance becomes invalid after disposal. Once disposed, any
    /// attempt to perform IMAP operations such as retrieving envelope or body
    /// structure will result in an <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    public class IMAP_Client_Message 
    {
        private bool                  m_IsDisposed    = false;
        private IMAP_Client           m_pImap;
        private long                  m_Uid           = 0;
        private long                  m_Size          = 0;
        private DateTime              m_InternalDate;
        private string[]              m_pFlags;
        private IMAP_t_Envelope?      m_pEnvelope      = null;
        private IMAP_t_BodyStructure? m_pBodyStructure = null;
        private string[]?             m_pGmailLabels   = null;
        private long?                 m_GmailMsgId     = null;
        private long?                 m_GmailThrId     = null;

        /// <summary>
        /// Initializes a new <see cref="IMAP_Client_Message"/> instance with the
        /// metadata obtained from an IMAP <c>FETCH</c> response.
        /// </summary>
        /// <param name="imap">
        /// The owning <see cref="IMAP_Client"/> instance used for any subsequent
        /// per‑message IMAP operations.
        /// </param>
        /// <param name="uid">
        /// The unique identifier (UID) of the message within the selected mailbox.
        /// </param>
        /// <param name="size">
        /// The RFC822 message size as returned by the <c>RFC822.SIZE</c> FETCH
        /// data item.
        /// </param>
        /// <param name="internalDate">
        /// The server‑assigned internal date of the message, corresponding to the
        /// <c>INTERNALDATE</c> FETCH data item.
        /// </param>
        /// <param name="flags">
        /// The message flags returned by the <c>FLAGS</c> FETCH data item.
        /// </param>
        /// <param name="envelope">
        /// The parsed message envelope, or <c>null</c> if envelope data was not
        /// prefetched during message‑set creation.
        /// </param>
        /// <param name="bodyStructure">
        /// The parsed MIME body structure, or <c>null</c> if body‑structure data
        /// was not prefetched during message‑set creation.
        /// </param>
        /// <param name="gmailLabels">
        /// The Gmail label list returned by the <c>X-GM-LABELS</c> extension, or
        /// <c>null</c> if Gmail metadata was not prefetched.
        /// </param>
        /// <param name="gmailMsgId">
        /// The immutable Gmail message identifier returned by <c>X-GM-MSGID</c>,
        /// or <c>null</c> if Gmail metadata was not prefetched.
        /// </param>
        /// <param name="gmailThrId">
        /// The Gmail thread identifier returned by <c>X-GM-THRID</c>, or
        /// <c>null</c> if Gmail metadata was not prefetched.
        /// </param>
        /// <remarks>
        /// <para>
        /// This constructor is used internally by <see cref="IMAP_Client_MessageSet"/>
        /// to materialize message objects from the results of a bulk FETCH operation.
        /// Any metadata not prefetched (such as envelope or body structure) remains
        /// <c>null</c> until explicitly retrieved via
        /// <see cref="GetEnvelopeAsync"/> or <see cref="GetBodyStructureAsync"/>.
        /// </para>
        /// <para>
        /// The message object does not perform any IMAP communication during
        /// construction; it simply stores the values provided by the FETCH response.
        /// </para>
        /// </remarks>
        internal IMAP_Client_Message(
            IMAP_Client imap,
            long uid,
            long size,
            DateTime internalDate,
            string[] flags,
            IMAP_t_Envelope? envelope,
            IMAP_t_BodyStructure? bodyStructure,
            string[]? gmailLabels,
            long? gmailMsgId,
            long? gmailThrId)
        {
            m_pImap          = imap;
            m_Uid            = uid;
            m_Size           = size;
            m_InternalDate   = internalDate;
            m_pFlags         = flags;
            m_pEnvelope      = envelope;
            m_pBodyStructure = bodyStructure;
            m_pGmailLabels   = gmailLabels;
            m_GmailMsgId     = gmailMsgId;
            m_GmailThrId     = gmailThrId;
        }


        #region method Dispose

        /// <summary>
        /// Marks this message instance as disposed and releases its internal state.
        /// </summary>
        internal void Dispose()
        {
            m_IsDisposed = true;
        }

        #endregion


        #region method Copy

        /// <summary>
        /// Synchronously copies this message to the specified target mailbox using the
        /// UID-based <c>COPY</c> command. This is a convenience wrapper around
        /// <see cref="CopyAsync(string,CancellationToken)"/> that executes the
        /// asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <param name="folder">
        /// The target mailbox name. Must not be <c>null</c> or empty.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>CopyAsync</c>
        /// operation completes. The source message remains unchanged; the server
        /// appends a copy of the message to the end of the target mailbox.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the copy operation or violates IMAP protocol
        /// semantics.
        /// </exception>
        public void Copy(string folder)
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            CopyAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method CopyAsync

        /// <summary>
        /// Copies this message to the specified target mailbox using the UID-based
        /// <c>COPY</c> command. This is a convenience wrapper around the IMAP‑client‑level
        /// <see cref="IMAP_Client.MessagesCopyAsync(IMAP_t_SeqSet,string,CancellationToken)"/>
        /// that operates on the UID of this message.
        /// </summary>
        /// <param name="folder">
        /// The target mailbox name. Must not be <c>null</c> or empty.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method constructs a UID sequence set containing only this
        /// message's UID and forwards the request to the underlying IMAP client.
        /// </para>
        /// <para>
        /// The source message remains unchanged. The server appends a copy of the
        /// message to the end of the target mailbox.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the copy operation or violates IMAP protocol
        /// semantics.
        /// </exception>
        public async ValueTask CopyAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(string.IsNullOrEmpty(folder)){
                throw new ArgumentException("Argument folder can't be null or empty.",nameof(folder));
            }

            await m_pImap.MessagesCopyAsync(IMAP_t_SeqSet.Parse(m_Uid.ToString()),folder,cancellationToken);
        }

        #endregion

        #region method Move

        /// <summary>
        /// Synchronously moves this message to the specified target mailbox by
        /// performing a UID-based <c>COPY</c> followed by adding the IMAP
        /// <c>\Deleted</c> flag to the original message. This is a convenience wrapper
        /// around <see cref="MoveAsync(string,CancellationToken)"/> that executes the
        /// asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <param name="folder">
        /// The target mailbox name. Must not be <c>null</c> or empty.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>MoveAsync</c>
        /// operation completes. The MOVE fallback consists of:
        /// </para>
        /// <list type="bullet">
        /// <item><description>UID <c>COPY</c> of this message to the target mailbox</description></item>
        /// <item><description>UID <c>STORE +FLAGS.SILENT (\Deleted)</c> on the original message</description></item>
        /// </list>
        /// <para>
        /// No <c>EXPUNGE</c> is performed. The message is not physically removed until
        /// the mailbox is expunged or closed, depending on server behavior.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects either the copy or flag operation.
        /// </exception>
        public void Move(string folder)
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            MoveAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MoveAsync

        /// <summary>
        /// Moves this message to the specified target mailbox by performing a UID-based
        /// <c>COPY</c> followed by adding the IMAP <c>\Deleted</c> flag to the original
        /// message. The message is not physically removed until the mailbox is expunged
        /// or closed.
        /// </summary>
        /// <param name="folder">
        /// The target mailbox name. Must not be <c>null</c> or empty.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method implements the standard IMAP MOVE fallback for servers that do
        /// not support the <c>MOVE</c> extension. The operation consists of:
        /// </para>
        /// <list type="bullet">
        /// <item><description>UID <c>COPY</c> of this message to the target mailbox</description></item>
        /// <item><description>UID <c>STORE +FLAGS.SILENT (\Deleted)</c> on the original message</description></item>
        /// </list>
        /// <para>
        /// No <c>EXPUNGE</c> is performed. This avoids unreliable sequence-number-based
        /// expunge notifications and ensures UID safety. The actual removal of the
        /// message occurs only when the mailbox is expunged (explicitly or implicitly
        /// on logout, depending on server behavior).
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects either the copy or flag operation.
        /// </exception>
        public async ValueTask MoveAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(string.IsNullOrEmpty(folder)){
                throw new ArgumentException("Argument folder can't be null or empty.",nameof(folder));
            }

            await CopyAsync(folder,cancellationToken);
            await MarkForDeletionAsync();
        }

        #endregion

        #region method MarkForDeletion

        /// <summary>
        /// Synchronously marks this message for deletion by adding the IMAP
        /// <c>\Deleted</c> flag using the UID-based <c>STORE</c> command. This is a
        /// convenience wrapper around
        /// <see cref="MarkForDeletionAsync(CancellationToken)"/> that executes the
        /// asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous
        /// <c>MarkForDeletionAsync</c> operation completes. The message is not physically
        /// removed until the mailbox is expunged or closed, depending on server
        /// behavior.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the flag operation or violates IMAP protocol
        /// semantics.
        /// </exception>
        public void MarkForDeletion()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            MarkForDeletionAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MarkForDeletionAsync

        /// <summary>
        /// Marks this message for deletion by adding the IMAP <c>\Deleted</c> flag using
        /// the UID-based <c>STORE</c> command. The message is not physically removed
        /// until the mailbox is expunged or closed.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method invokes <see cref="StoreFlagsAsync(IMAP_t_Store_FlagsMode,string[],CancellationToken)"/>
        /// with <see cref="IMAP_t_Store_FlagsMode.AddSilent"/> and the <c>\Deleted</c>
        /// flag. The actual removal of the message occurs only when the mailbox is
        /// expunged (either explicitly or implicitly when the server performs an
        /// auto‑expunge on logout).
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the flag operation or violates IMAP protocol
        /// semantics.
        /// </exception>
        public async ValueTask MarkForDeletionAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            await StoreFlagsAsync(IMAP_t_Store_FlagsMode.AddSilent,new[]{"\\Deleted"},cancellationToken);
        }

        #endregion

        #region method StoreFlags

        /// <summary>
        /// Synchronously applies the specified IMAP message flags to this message using
        /// the UID-based <c>STORE</c> command. This is a convenience wrapper around
        /// <see cref="StoreFlagsAsync(IMAP_t_Store_FlagsMode,string[],CancellationToken)"/>
        /// that executes the asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <param name="flagsMode">
        /// The flag operation mode (replace, add, remove, and their SILENT variants).
        /// </param>
        /// <param name="flags">
        /// The flags to apply. Must not be <c>null</c>.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>StoreFlagsAsync</c>
        /// operation completes. All IMAP protocol validation and safety checks are
        /// performed by the asynchronous implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the flag operation or violates IMAP protocol
        /// semantics.
        /// </exception>
        public void StoreFlags(IMAP_t_Store_FlagsMode flagsMode,string[] flags)
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            StoreFlagsAsync(flagsMode,flags,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method StoreFlagsAsync        

        /// <summary>
        /// Applies the specified IMAP message flags to this message using the UID-based
        /// <c>STORE</c> command. This is a convenience wrapper around the IMAP‑client‑level
        /// <see cref="IMAP_Client.MessagesStoreFlagsUidAsync(IMAP_t_SeqSet,IMAP_t_Store_FlagsMode,string[],CancellationToken)"/>
        /// that operates on the UID of this message.
        /// </summary>
        /// <param name="flagsMode">
        /// The flag operation mode (replace, add, remove, and their SILENT variants).
        /// </param>
        /// <param name="flags">
        /// The flags to apply. Must not be <c>null</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method constructs a UID sequence set containing only this
        /// message's UID and forwards the request to the underlying IMAP client.
        /// </para>
        /// <para>
        /// All IMAP protocol validation and safety checks are performed by the
        /// underlying <c>MessagesStoreFlagsUidAsync</c> implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the flag operation or violates IMAP protocol
        /// semantics.
        /// </exception>
        public async ValueTask StoreFlagsAsync(IMAP_t_Store_FlagsMode flagsMode,string[] flags,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }                        
            if(flags == null){
                throw new ArgumentNullException(nameof(flags));
            }

            await m_pImap.MessagesStoreFlagsUidAsync(IMAP_t_SeqSet.Parse(m_Uid.ToString()),flagsMode,flags,cancellationToken);
        }

        #endregion

        #region method HeaderToMailMessage

        /// <summary>
        /// Synchronously retrieves the IMAP <c>BODY[HEADER]</c> section of this message,
        /// parses it, and returns a <see cref="Mail_Message"/> instance containing only
        /// the RFC 5322 header block. This is a convenience wrapper around
        /// <see cref="HeaderToMailMessageAsync(CancellationToken)"/> that executes the
        /// asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous
        /// <c>HeaderToMailMessageAsync</c> operation completes. All IMAP protocol
        /// validation and safety checks—including enforcement of the maximum allowed
        /// literal size (the message's reported <c>RFC822.SIZE</c> plus a 1 MB
        /// margin)—are performed by the asynchronous implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public Mail_Message HeaderToMailMessage()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return HeaderToMailMessageAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToMailMessageAsync

        /// <summary>
        /// Retrieves the IMAP <c>BODY[HEADER]</c> section of this message, parses it, and
        /// returns a <see cref="Mail_Message"/> instance containing only the RFC 5322
        /// header block. This does not download or parse the message body.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method streams the header data into a <see cref="MemoryStreamEx"/>
        /// using <see cref="HeaderToStreamAsync(Stream,CancellationToken)"/> and then parses
        /// the resulting data via <see cref="Mail_Message.ParseFromStream(Stream)"/>.
        /// </para>
        /// <para>
        /// All IMAP protocol validation and safety checks—including enforcement of the
        /// maximum allowed literal size (the message's reported <c>RFC822.SIZE</c> plus a
        /// 1 MB margin)—are performed by the underlying streaming implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
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
        /// Synchronously retrieves the IMAP <c>BODY[HEADER]</c> section of this message
        /// and returns it as a UTF‑8 string. This is a convenience wrapper around
        /// <see cref="HeaderToStringAsync(CancellationToken)"/> that executes the
        /// asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>HeaderToStringAsync</c>
        /// operation completes. All IMAP protocol validation and safety checks—including
        /// enforcement of the maximum allowed literal size (the message's reported
        /// <c>RFC822.SIZE</c> plus a 1 MB margin)—are performed by the asynchronous
        /// implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public string HeaderToString()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return HeaderToStringAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToStringAsync

        /// <summary>
        /// Retrieves the IMAP <c>BODY[HEADER]</c> section of this message and returns it
        /// as a UTF‑8 string. This downloads only the RFC 5322 header block without
        /// fetching the message body.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method obtains the header bytes via
        /// <see cref="HeaderToByteAsync(CancellationToken)"/> and decodes them using
        /// UTF‑8.
        /// </para>
        /// <para>
        /// All IMAP protocol validation and safety checks—including enforcement of the
        /// maximum allowed literal size (the message's reported <c>RFC822.SIZE</c> plus a
        /// 1 MB margin)—are performed by the underlying streaming implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public async ValueTask<string> HeaderToStringAsync(CancellationToken cancellationToken = default)
        {
            byte[] header = await HeaderToByteAsync(cancellationToken);
            
            return Encoding.UTF8.GetString(header);
        }

        #endregion

        #region method HeaderToByte

        /// <summary>
        /// Synchronously retrieves the IMAP <c>BODY[HEADER]</c> section of this message
        /// and returns it as a byte array. This is a convenience wrapper around
        /// <see cref="HeaderToByteAsync(CancellationToken)"/> that executes the asynchronous
        /// operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>HeaderToByteAsync</c>
        /// operation completes. All IMAP protocol validation and safety checks—including
        /// enforcement of the maximum allowed literal size (the message's reported
        /// <c>RFC822.SIZE</c> plus a 1 MB margin)—are performed by the asynchronous
        /// implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public byte[] HeaderToByte()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return HeaderToByteAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToByteAsync

        /// <summary>
        /// Retrieves the IMAP <c>BODY[HEADER]</c> section of this message and returns it
        /// as a byte array. This downloads only the RFC 5322 header block without
        /// fetching the message body.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method streams the header data into a <see cref="MemoryStream"/>
        /// using <see cref="HeaderToStreamAsync(Stream,CancellationToken)"/> and then
        /// returns the resulting byte array.
        /// </para>
        /// <para>
        /// All IMAP protocol validation and safety checks—including enforcement of the
        /// maximum allowed literal size (the message's reported <c>RFC822.SIZE</c> plus a
        /// 1 MB margin)—are performed by the underlying streaming implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public async ValueTask<byte[]> HeaderToByteAsync(CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();

            await HeaderToStreamAsync(stream,cancellationToken);

            return stream.ToArray();
        }

        #endregion

        #region method HeaderToStream

        /// <summary>
        /// Synchronously streams the IMAP <c>BODY[HEADER]</c> section of this message into
        /// the provided <paramref name="stream"/>. This is a convenience wrapper around
        /// <see cref="HeaderToStreamAsync(Stream,CancellationToken)"/> that executes the
        /// asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <param name="stream">
        /// The target stream that receives the header data. Must not be <c>null</c>.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>HeaderToStreamAsync</c>
        /// operation completes. All IMAP protocol validation and safety checks—including
        /// enforcement of the maximum allowed literal size (the message's reported
        /// <c>RFC822.SIZE</c> plus a 1 MB margin)—are performed by the asynchronous
        /// implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public void HeaderToStream(Stream stream)
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            HeaderToStreamAsync(stream,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method HeaderToStreamAsync

        /// <summary>
        /// Streams the IMAP <c>BODY[HEADER]</c> section of this message into the provided
        /// <paramref name="stream"/>. This retrieves only the message header fields
        /// (RFC 5322 header block) without downloading the message body.
        /// </summary>
        /// <param name="stream">
        /// The target stream that receives the header data. Must not be <c>null</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method is equivalent to calling
        /// <see cref="BodyToStreamAsync(Stream,string?,long?,long?,CancellationToken)"/>
        /// with the section specifier <c>"HEADER"</c>, causing the server to return only
        /// the message header portion.
        /// </para>
        /// <para>
        /// All IMAP protocol validation and safety checks—including enforcement of the
        /// maximum allowed literal size (the message's reported <c>RFC822.SIZE</c> plus a
        /// 1 MB margin)—are performed by the underlying streaming implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public async ValueTask HeaderToStreamAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            await BodyToStreamAsync(stream,"HEADER",null,null,cancellationToken);
        }

        #endregion

        #region method ToMailMessage

        /// <summary>
        /// Synchronously retrieves the full IMAP <c>BODY[]</c> content of this message,
        /// parses it, and returns a <see cref="Mail_Message"/> instance. This is a
        /// convenience wrapper around <see cref="ToMailMessageAsync(CancellationToken)"/>
        /// that executes the asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>ToMailMessageAsync</c>
        /// operation completes. All IMAP protocol validation and safety checks—including
        /// enforcement of the maximum allowed literal size (the message's reported
        /// <c>RFC822.SIZE</c> plus a 1 MB margin)—are performed by the asynchronous
        /// implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public Mail_Message ToMailMessage()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return ToMailMessageAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToMailMessageAsync

        /// <summary>
        /// Retrieves the full IMAP <c>BODY[]</c> content of this message, parses it, and
        /// returns a <see cref="Mail_Message"/> instance. This method downloads the entire
        /// message and constructs an in‑memory representation of its MIME structure.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method streams the message into a <see cref="MemoryStreamEx"/>
        /// using <see cref="ToStreamAsync(Stream,CancellationToken)"/> and then parses the
        /// resulting data via <see cref="Mail_Message.ParseFromStream(Stream)"/>.
        /// </para>
        /// <para>
        /// All IMAP protocol validation and safety checks—including enforcement of the
        /// maximum allowed literal size (the message's reported <c>RFC822.SIZE</c> plus a
        /// 1 MB margin)—are performed by the underlying streaming implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
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
        /// Synchronously retrieves the full IMAP <c>BODY[]</c> content of this message and
        /// returns it as a byte array. This is a convenience wrapper around
        /// <see cref="ToByteAsync(CancellationToken)"/> that executes the asynchronous
        /// operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>ToByteAsync</c>
        /// operation completes. All IMAP protocol validation and safety checks—including
        /// enforcement of the maximum allowed literal size (the message's reported
        /// <c>RFC822.SIZE</c> plus a 1 MB margin)—are performed by the asynchronous
        /// implementation.
        /// </para>
        /// <para>
        /// <b>Warning:</b> This method buffers the entire message in memory. For large
        /// messages or memory‑constrained environments, prefer
        /// <see cref="ToStream(Stream)"/> or <see cref="ToStreamAsync(Stream,CancellationToken)"/>
        /// to stream the message directly to a file or processing pipeline.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public byte[] ToByte()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return ToByteAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToByteAsync

        /// <summary>
        /// Retrieves the full IMAP <c>BODY[]</c> content of this message and returns it as
        /// a byte array. This method buffers the entire message in memory and should only
        /// be used when the message size is known to be reasonably small.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// Internally, this method streams the message into a <see cref="MemoryStream"/>
        /// using <see cref="ToStreamAsync(Stream,CancellationToken)"/> and then returns
        /// the resulting byte array.
        /// </para>
        /// <para>
        /// <b>Warning:</b> For large messages, using this method may result in significant
        /// memory usage because the entire message is stored in RAM. When working with
        /// large messages or when memory usage is a concern, prefer
        /// <see cref="ToStreamAsync(Stream,CancellationToken)"/> or <see cref="ToStream(Stream)"/>
        /// to stream the message directly to a file or processing pipeline.
        /// </para>
        /// <para>
        /// All IMAP protocol validation and safety checks—including enforcement of the
        /// maximum allowed literal size (the message's reported <c>RFC822.SIZE</c> plus a
        /// 1 MB margin)—are performed by the underlying streaming implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public async ValueTask<byte[]> ToByteAsync(CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();

            await ToStreamAsync(stream,cancellationToken);

            return stream.ToArray();
        }

        #endregion

        #region method ToStream
        
        /// <summary>
        /// Synchronously streams the full IMAP <c>BODY[]</c> content of this message into
        /// the provided <paramref name="stream"/>. This is a convenience wrapper around
        /// <see cref="ToStreamAsync(Stream,CancellationToken)"/> that executes the
        /// asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <param name="stream">
        /// The target stream that receives the message data. Must not be <c>null</c>.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous <c>ToStreamAsync</c>
        /// operation completes. All IMAP protocol validation and safety checks—including
        /// enforcement of the maximum allowed literal size (the message's reported
        /// <c>RFC822.SIZE</c> plus a 1 MB margin)—are performed by the asynchronous
        /// implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public void ToStream(Stream stream)
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            ToStreamAsync(stream,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToStreamAsync
        
        /// <summary>
        /// Streams the full IMAP <c>BODY[]</c> content of this message into the provided
        /// <paramref name="stream"/>. This is a convenience method that performs a complete
        /// message fetch without specifying any section, offset, or size constraints.
        /// </summary>
        /// <param name="stream">
        /// The target stream that receives the message data. Must not be <c>null</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method is equivalent to calling
        /// <see cref="BodyToStreamAsync(Stream,string?,long?,long?,CancellationToken)"/>
        /// with a <c>null</c> section specifier and no partial‑fetch parameters, causing
        /// the server to return the full <c>BODY[]</c> literal.
        /// </para>
        /// <para>
        /// All protocol validation and safety checks—including the enforcement of the
        /// maximum allowed literal size (the message's reported <c>RFC822.SIZE</c> plus
        /// a 1 MB margin)—are handled by the underlying asynchronous implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public async ValueTask ToStreamAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            await BodyToStreamAsync(stream,null,null,null,cancellationToken);
        }

        #endregion

        #region method BodyToStream

        /// <summary>
        /// Synchronously streams the specified IMAP <c>BODY</c> section of this message into
        /// the provided <paramref name="stream"/>. This is a convenience wrapper around
        /// <see cref="BodyToStreamAsync(Stream,string?,long?,long?,CancellationToken)"/> that
        /// executes the asynchronous operation using the IMAP client’s configured timeout.
        /// </summary>
        /// <param name="stream">
        /// The target stream that receives the message data. Must not be <c>null</c>.
        /// </param>
        /// <param name="partSpecifier">
        /// The IMAP <c>BODY</c> section specifier. Use <c>null</c> for <c>BODY[]</c> (full
        /// message), or a section identifier such as <c>"1"</c>, <c>"2.MIME"</c>, etc.
        /// </param>
        /// <param name="offset">
        /// Optional byte offset for partial fetch. When specified together with
        /// <paramref name="maxSize"/>, the server is requested to return a partial literal
        /// (<c>BODY[]&lt;offset.maxSize&gt;</c>). Use <c>null</c> for full fetch.
        /// </param>
        /// <param name="maxSize">
        /// Optional maximum number of bytes to request for partial fetch. Ignored when
        /// <paramref name="offset"/> is <c>null</c>.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method simply invokes the asynchronous <c>BodyToStreamAsync</c> operation
        /// and blocks until completion. All protocol validation, literal-size checks, and
        /// safety limits (including the rule that literal size must not exceed the message's
        /// reported <c>RFC822.SIZE</c> plus a 1 MB margin) are enforced by the underlying
        /// asynchronous implementation.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public void BodyToStream(Stream stream,string? partSpecifier,long? offset,long? maxSize)
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            BodyToStreamAsync(stream,partSpecifier,offset,maxSize,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method BodyToStreamAsync

        /// <summary>
        /// Streams the specified IMAP <c>BODY</c> section of this message into the provided
        /// <paramref name="stream"/>. Supports full-message fetch (<c>BODY[]</c>), MIME part
        /// fetch (<c>BODY[section]</c>), and partial fetch (<c>BODY[]&lt;offset.maxSize&gt;</c>).
        /// </summary>
        /// <param name="stream">
        /// The target stream that receives the message data. The caller is responsible for
        /// positioning and lifetime management of the stream. Must not be <c>null</c>.
        /// </param>
        /// <param name="partSpecifier">
        /// The IMAP <c>BODY</c> section specifier. Use <c>null</c> for <c>BODY[]</c> (full
        /// message), or a section identifier such as <c>"1"</c>, <c>"2.MIME"</c>, etc.
        /// </param>
        /// <param name="offset">
        /// Optional byte offset for partial fetch. When specified together with
        /// <paramref name="maxSize"/>, the server is requested to return a partial literal
        /// (<c>BODY[]&lt;offset.maxSize&gt;</c>). Use <c>null</c> for full fetch.
        /// </param>
        /// <param name="maxSize">
        /// Optional maximum number of bytes to request for partial fetch. When used with
        /// <paramref name="offset"/>, limits the literal size returned by the server.
        /// When <paramref name="offset"/> is <c>null</c>, this value is ignored.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// The method performs a single IMAP <c>FETCH</c> command and streams the literal
        /// data directly into <paramref name="stream"/> via the <c>IMAP_e_Fetch_GetStoreStream</c>
        /// callback. No buffering of the message content is performed by the client.
        /// </para>
        /// <para>
        /// A protocol exception is thrown if the server returns an <c>OK</c> completion
        /// response without providing the expected <c>FETCH</c> data item.
        /// </para>
        /// <para>
        /// <b>Safety considerations:</b>
        /// IMAP literals include an explicit size prefix (<c>{N}</c>). Servers are expected
        /// to send exactly <c>N</c> bytes, but intermediary systems (spam filters, antivirus
        /// gateways, proxies) may rewrite message content without updating <c>RFC822.SIZE</c>.
        /// To guard against malformed or hostile server behavior, implementations should
        /// enforce an upper bound on the literal size. This client limits the allowed literal
        /// size to the message's reported <c>RFC822.SIZE</c> plus a 1 MB safety margin.
        /// Any literal exceeding this threshold is rejected to prevent excessive resource
        /// usage or protocol desynchronization.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the message instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server violates IMAP protocol semantics, such as returning
        /// <c>OK</c> without a corresponding <c>FETCH</c> response, or if the literal size
        /// exceeds the allowed safety limit.
        /// </exception>
        public async Task BodyToStreamAsync(Stream stream,string? partSpecifier,long? offset,long? maxSize,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            bool receivedResponse = false;
            EventHandler<IMAP_r_u_Fetch> callback = delegate(object? sender,IMAP_r_u_Fetch e){
                if(e.Body != null){
                    receivedResponse = true;
                }
            };

            EventHandler<IMAP_e_Fetch_GetStoreStream> getStoreStreamCallback = delegate(object? sender,IMAP_e_Fetch_GetStoreStream e){
                if(e.DataLength > (m_Size + (1000 * 1000))){
                    double actualMB   = e.DataLength / 1024d / 1024d;
                    double reportedMB = m_Size / 1024d / 1024d;

                    throw new IMAP_ProtocolException(
                        $"IMAP protocol violation: literal size {e.DataLength} bytes ({actualMB:F2} MB) " +
                        $"exceeds reported message size {m_Size} bytes ({reportedMB:F2} MB) " +
                        $"by more than the allowed tolerance."
                    );
                }
                e.Stream = stream;
            };

            await m_pImap.MessagesFetchUidAsync(
                new IMAP_t_SeqSet(m_Uid),
                new []{new IMAP_t_Fetch_i_Body(partSpecifier,offset,maxSize)},
                callback,
                getStoreStreamCallback,
                cancellationToken
            );

            // Server returned OK final response, but no requested fetch response.
            if(!receivedResponse){
                throw new IMAP_ProtocolException("IMAP protocol violation: Server returned OK but no FETCH response for the requested message.");
            }
        }

        #endregion

        #region method GetEnvelope

        /// <summary>
        /// Retrieves the message envelope for this message using a synchronous
        /// blocking call. This method wraps <see cref="GetEnvelopeAsync"/> and
        /// waits for its completion using the IMAP client’s configured timeout.
        /// </summary>
        /// <returns>
        /// The parsed <see cref="IMAP_t_Envelope"/> instance describing the
        /// message’s header fields.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server returns a successful completion response but does
        /// not include the required <c>FETCH</c> data item for the requested
        /// envelope.
        /// </exception>
        /// <remarks>
        /// <para>
        /// If the envelope was already populated during message‑set creation
        /// (via <c>preFetchEnvelope</c>), the cached value is returned immediately
        /// without performing any IMAP communication.
        /// </para>
        /// <para>
        /// When the envelope has not been prefetched, this method performs a
        /// synchronous wait on <see cref="GetEnvelopeAsync"/>. A
        /// <see cref="CancellationTokenSource"/> is created using the IMAP client’s
        /// timeout value to ensure the operation does not block indefinitely.
        /// </para>
        /// <para>
        /// This method is intended for callers that require a synchronous API.
        /// Asynchronous code should prefer <see cref="GetEnvelopeAsync"/> to avoid
        /// blocking threads.
        /// </para>
        /// </remarks>
        public IMAP_t_Envelope GetEnvelope()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return GetEnvelopeAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method GetEnvelopeAsync

        /// <summary>
        /// Retrieves the message envelope for this message. If the envelope was
        /// already populated during message‑set creation (via <c>preFetchEnvelope</c>),
        /// the cached value is returned immediately; otherwise a dedicated IMAP
        /// <c>FETCH</c> request is issued to obtain the envelope from the server.
        /// </summary>
        /// <returns>
        /// The parsed <see cref="IMAP_t_Envelope"/> instance describing the message’s
        /// header fields.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server returns a successful completion response but does not
        /// include the required <c>FETCH</c> data item for the requested envelope.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Envelope data may be prefetched when the containing
        /// <see cref="IMAP_Client_MessageSet"/> is created. In that case, this method
        /// simply returns the already‑available envelope without performing any IMAP
        /// communication.
        /// </para>
        /// <para>
        /// If the envelope was not prefetched, this method performs a targeted
        /// <c>FETCH UID (ENVELOPE)</c> request for the message’s UID. The result is
        /// cached so subsequent calls do not trigger additional server round‑trips.
        /// </para>
        /// <para>
        /// The envelope structure contains the message’s primary header fields such as
        /// date, subject, sender, recipients, and message identifiers.
        /// </para>
        /// </remarks>
        public async Task<IMAP_t_Envelope> GetEnvelopeAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            if(m_pEnvelope != null){
                return m_pEnvelope;
            }

            EventHandler<IMAP_r_u_Fetch> callback = delegate(object? sender,IMAP_r_u_Fetch e){
                if(e.Envelope != null){
                    m_pEnvelope = e.Envelope.Envelope;
                }
            };

            await m_pImap.MessagesFetchUidAsync(
                IMAP_t_SeqSet.Parse(m_Uid.ToString()),
                new []{new IMAP_t_Fetch_i_Envelope()},
                callback,
                null,
                cancellationToken
            );

            // Server returned OK final response, but no requested fetch response.
            if(m_pEnvelope == null){
                throw new IMAP_ProtocolException("IMAP protocol violation: Server returned OK but no FETCH response for the requested message.");
            }

            return m_pEnvelope;
        }

        #endregion

        #region method GetBodyStructure

        /// <summary>
        /// Retrieves the MIME body structure for this message using a synchronous
        /// blocking call. This method wraps <see cref="GetBodyStructureAsync"/> and
        /// waits for its completion using the IMAP client’s configured timeout.
        /// </summary>
        /// <returns>
        /// The parsed <see cref="IMAP_t_BodyStructure"/> instance describing the
        /// message’s MIME tree.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server returns a successful completion response but does
        /// not include the required <c>FETCH</c> data item for the requested body
        /// structure.
        /// </exception>
        /// <remarks>
        /// <para>
        /// If the body structure was already populated during message‑set creation
        /// (via <c>preFetchBodyStructure</c>), the cached value is returned
        /// immediately without performing any IMAP communication.
        /// </para>
        /// <para>
        /// When the body structure has not been prefetched, this method performs a
        /// synchronous wait on <see cref="GetBodyStructureAsync"/>. A
        /// <see cref="CancellationTokenSource"/> is created using the IMAP client’s
        /// timeout value to ensure the operation does not block indefinitely.
        /// </para>
        /// <para>
        /// This method is intended for callers that require a synchronous API.
        /// Asynchronous code should prefer <see cref="GetBodyStructureAsync"/> to
        /// avoid blocking threads.
        /// </para>
        /// </remarks>
        public IMAP_t_BodyStructure GetBodyStructure()
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return GetBodyStructureAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method GetBodyStructureAsync

        /// <summary>
        /// Retrieves the MIME body structure for this message. If the body
        /// structure was already populated during message‑set creation (via
        /// <c>preFetchBodyStructure</c>), the cached value is returned immediately;
        /// otherwise a dedicated IMAP <c>FETCH</c> request is issued to obtain the
        /// structure from the server.
        /// </summary>
        /// <returns>
        /// The parsed <see cref="IMAP_t_BodyStructure"/> instance describing the
        /// message’s MIME tree.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server returns a successful completion response but does
        /// not include the required <c>FETCH</c> data item for the requested body
        /// structure.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Body‑structure data may be prefetched when the containing
        /// <see cref="IMAP_Client_MessageSet"/> is created. In that case, this
        /// method returns the already‑available structure without performing any
        /// IMAP communication.
        /// </para>
        /// <para>
        /// If the body structure was not prefetched, this method performs a
        /// targeted <c>FETCH UID (BODYSTRUCTURE)</c> request for the message’s UID.
        /// The result is cached so subsequent calls do not trigger additional
        /// server round‑trips.
        /// </para>
        /// <para>
        /// The body structure describes the full MIME layout of the message,
        /// including multipart boundaries, content types, encodings, and part
        /// hierarchy. It does not include header fields; use
        /// <see cref="GetEnvelopeAsync"/> to retrieve the message envelope.
        /// </para>
        /// </remarks>
        public async Task<IMAP_t_BodyStructure> GetBodyStructureAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            if(m_pBodyStructure != null){
                return m_pBodyStructure;
            }

            EventHandler<IMAP_r_u_Fetch> callback = delegate(object? sender,IMAP_r_u_Fetch e){
                if(e.BodyStructure != null){
                    m_pBodyStructure = e.BodyStructure.BodyStructure;
                }
            };

            await m_pImap.MessagesFetchUidAsync(
                IMAP_t_SeqSet.Parse(m_Uid.ToString()),
                new []{new IMAP_t_Fetch_i_BodyStructure()},
                callback,
                null,
                cancellationToken
            );

            // Server returned OK final response, but no requested fetch response.
            if(m_pBodyStructure == null){
                throw new IMAP_ProtocolException("IMAP protocol violation: Server returned OK but no FETCH response for the requested message.");
            }

            return m_pBodyStructure;
        }

        #endregion

        #region method ContainsFlag

        /// <summary>
        /// Determines whether this message has the specified IMAP flag, using a
        /// case‑insensitive comparison.
        /// </summary>
        /// <param name="flag">
        /// The flag name to check. This may be a system flag (such as
        /// <c>\Seen</c>, <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// or <c>\Draft</c>) or a user‑defined keyword. The value must not be
        /// <c>null</c> or empty.
        /// </param>
        /// <returns>
        /// <c>true</c> if the message’s flag list contains the specified flag;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="flag"/> is <c>null</c> or an empty string.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The comparison is performed using
        /// <see cref="StringComparison.OrdinalIgnoreCase"/>, ensuring that flag
        /// matching is not affected by case differences. IMAP servers typically
        /// return system flags in uppercase with a leading backslash, but keyword
        /// flags may vary in casing depending on the server implementation.
        /// </para>
        /// <para>
        /// This method performs a simple linear scan of the message’s flag array.
        /// Since IMAP messages rarely contain more than a handful of flags, this
        /// approach is efficient and avoids the overhead of LINQ or additional
        /// data structures.
        /// </para>
        /// </remarks>
        public bool ContainsFlag(string flag)
        {
            if(string.IsNullOrEmpty(flag)){
                throw new ArgumentException("Flag value can't be null or empty.",nameof(flag));
            }

            foreach(string f in m_pFlags){
                if(string.Equals(f, flag, StringComparison.OrdinalIgnoreCase)){
                    return true;
                }
            }
            return false;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this message instance has been disposed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <c>true</c>, the message object has released its internal resources and
        /// can no longer be used for IMAP operations. Any attempt to call methods such
        /// as <c>Copy</c>, <c>Move</c>, <c>StoreFlags</c>, or <c>MarkForDeletion</c>
        /// will result in an <see cref="ObjectDisposedException"/>.
        /// </para>
        /// <para>
        /// This property allows callers to check the disposal state before performing
        /// operations that require an active IMAP session or valid message context.
        /// </para>
        /// </remarks>
        public bool IsDisposed
        {
            get { return m_IsDisposed; }
        }

        /// <summary>
        /// Gets a value indicating whether this message is marked for deletion.
        /// This corresponds to the presence of the IMAP <c>\Deleted</c> system flag.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A message marked with <c>\Deleted</c> is considered flagged for removal
        /// by the IMAP server. The message is not actually removed until an
        /// <c>EXPUNGE</c> command is issued on the mailbox. Until then, the message
        /// remains accessible and may still be fetched or manipulated.
        /// </para>
        /// <para>
        /// This property performs a case‑insensitive check for the <c>\Deleted</c>
        /// flag using <see cref="ContainsFlag(string)"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public bool IsMarkedForDeletion
        {
            get { 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return ContainsFlag("\\Deleted"); 
            }
        }

        /// <summary>
        /// Gets the unique identifier (UID) of this message within the currently
        /// selected mailbox. The UID is a stable, server‑assigned value that does not
        /// change for the lifetime of the message.
        /// </summary>
        /// <remarks>
        /// <para>
        /// IMAP UIDs provide a reliable way to reference messages across sessions and
        /// are preferred over sequence numbers, which may change whenever the mailbox
        /// state is modified. All message‑level operations in this class use UIDs to
        /// ensure consistency and avoid issues caused by sequence‑number shifts.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public long Uid
        {
            get { 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_Uid; 
            }
        }

        /// <summary>
        /// Gets the RFC822 size of this message in bytes, as reported by the IMAP
        /// server. The value corresponds to the <c>RFC822.SIZE</c> FETCH data item.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The size indicates the total number of bytes in the raw RFC822 message,
        /// including headers and body. It is useful for pre‑allocating storage,
        /// estimating download cost, or applying limits when retrieving message
        /// content.
        /// </para>
        /// <para>
        /// The value is provided by the server and may not always match the exact
        /// literal size returned during a FETCH operation, although such discrepancies
        /// are rare. The client uses this value as a guideline when handling literal
        /// data and applying optional storage limits.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public long Size
        {
            get { 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_Size; 
            }
        }

        /// <summary>
        /// Gets the internal date of this message as reported by the IMAP server.
        /// The value corresponds to the <c>INTERNALDATE</c> FETCH data item.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The internal date is the timestamp assigned to the message by the server
        /// when it was added to the mailbox. It typically reflects the message’s
        /// delivery or arrival time rather than the date found in the message headers.
        /// </para>
        /// <para>
        /// IMAP servers guarantee that <c>INTERNALDATE</c> is stable and does not
        /// change for the lifetime of the message. It is commonly used for sorting,
        /// synchronization, and determining message age.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public DateTime InternalDate
        {
            get {                 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_InternalDate; 
            }
        }

        /// <summary>
        /// Gets the set of IMAP flags currently associated with this message. These
        /// correspond to the <c>FLAGS</c> FETCH data item returned by the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Message flags indicate the state of the message within the mailbox. Common
        /// system flags include <c>\Seen</c>, <c>\Answered</c>, <c>\Flagged</c>,
        /// <c>\Deleted</c>, and <c>\Draft</c>. Servers may also provide custom
        /// keywords to represent additional message metadata.
        /// </para>
        /// <para>
        /// The flag set reflects the server’s state at the time the message was
        /// fetched. Changes made through operations such as <c>StoreFlags</c> or
        /// <c>MarkForDeletion</c> will update the server and may cause this property
        /// to change when the message is refreshed or re-fetched.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public string[] Flags
        {
            get {                 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pFlags; 
            }
        }

        /// <summary>
        /// Gets the message envelope if it has already been retrieved.
        /// </summary>
        /// <returns>
        /// The cached <see cref="IMAP_t_Envelope"/> instance, or <c>null</c> if the
        /// envelope has not yet been fetched.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This property exposes the envelope only if it was populated during
        /// message‑set creation (via <c>preFetchEnvelope</c>) or after a successful
        /// call to <see cref="GetEnvelopeAsync"/> or <see cref="GetEnvelope"/>.
        /// </para>
        /// <para>
        /// Accessing this property does not trigger any IMAP communication. If the
        /// envelope has not been fetched yet, use <see cref="GetEnvelopeAsync"/> or
        /// <see cref="GetEnvelope"/> to retrieve it from the server.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public IMAP_t_Envelope? Envelope
        {
            get {                 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pEnvelope; 
            }
        }

        /// <summary>
        /// Gets the MIME body structure for this message if it has already been
        /// retrieved.
        /// </summary>
        /// <returns>
        /// The cached <see cref="IMAP_t_BodyStructure"/> instance, or <c>null</c>
        /// if the body structure has not yet been fetched.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This property exposes the body structure only if it was populated during
        /// message‑set creation (via <c>preFetchBodyStructure</c>) or after a
        /// successful call to <see cref="GetBodyStructureAsync"/> or
        /// <see cref="GetBodyStructure"/>.
        /// </para>
        /// <para>
        /// Accessing this property does not trigger any IMAP communication. If the
        /// body structure has not been fetched yet, use
        /// <see cref="GetBodyStructureAsync"/> or <see cref="GetBodyStructure"/> to
        /// retrieve it from the server.
        /// </para>
        /// <para>
        /// The body structure describes the full MIME layout of the message,
        /// including multipart structure, content types, encodings, and part
        /// hierarchy.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public IMAP_t_BodyStructure? BodyStructure
        {
            get {                 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pBodyStructure; 
            }
        }

        /// <summary>
        /// Gets the Gmail label list associated with this message, as returned by
        /// the <c>X-GM-LABELS</c> FETCH data item.
        /// </summary>
        /// <returns>
        /// An array of UTF‑8 label strings, or <c>null</c> if Gmail label metadata
        /// was not requested when the containing <c>MessageSet</c> was created.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Gmail exposes message labels through the non‑standard
        /// <c>X-GM-LABELS</c> extension. The values returned here correspond to the
        /// labels transmitted by the server during the initial FETCH operation.
        /// </para>
        /// <para>
        /// This property is populated only when <c>preFetchGmail</c> was enabled in
        /// <c>CreateMessageSetAsync</c>. If Gmail extensions were not prefetched,
        /// the property remains <c>null</c> and no additional server requests are
        /// performed to retrieve label information.
        /// </para>
        /// <para>
        /// The label list may include both system labels (such as <c>\Inbox</c>,
        /// <c>\Important</c>, or <c>\Starred</c>) and user‑defined labels created
        /// within Gmail.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public string[]? GmailLabels
        {
            get {                 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pGmailLabels; 
            }
        }

        /// <summary>
        /// Gets the immutable Gmail message identifier associated with this
        /// message, as returned by the <c>X-GM-MSGID</c> FETCH data item.
        /// </summary>
        /// <returns>
        /// The 64‑bit Gmail message identifier, or <c>null</c> if Gmail metadata
        /// was not requested when the containing <c>MessageSet</c> was created.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <c>X-GM-MSGID</c> is a Gmail‑specific extension that provides a stable,
        /// globally unique identifier for the lifetime of the message. Unlike IMAP
        /// UIDs, which may change when messages are moved or when UIDVALIDITY is
        /// reset, the Gmail message ID is immutable and consistent across all
        /// folders.
        /// </para>
        /// <para>
        /// This property is populated only when <c>preFetchGmail</c> was enabled in
        /// <c>CreateMessageSetAsync</c>. If Gmail extensions were not prefetched,
        /// the property remains <c>null</c> and no additional server requests are
        /// performed to retrieve the value.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public long? GmailMsgId
        {
            get { 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_GmailMsgId; 
            }
        }

        /// <summary>
        /// Gets the Gmail thread identifier associated with this message, as
        /// returned by the <c>X-GM-THRID</c> FETCH data item.
        /// </summary>
        /// <returns>
        /// The 64‑bit Gmail thread identifier, or <c>null</c> if Gmail metadata
        /// was not requested when the containing <c>MessageSet</c> was created.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <c>X-GM-THRID</c> is a Gmail‑specific extension that provides the stable
        /// thread ID used by Gmail to group related messages into a conversation.
        /// All messages that Gmail considers part of the same conversation share
        /// the same thread identifier.
        /// </para>
        /// <para>
        /// This property is populated only when <c>preFetchGmail</c> was enabled in
        /// <c>CreateMessageSetAsync</c>. If Gmail extensions were not prefetched,
        /// the property remains <c>null</c> and no additional server requests are
        /// performed to retrieve the value.
        /// </para>
        /// <para>
        /// The Gmail thread ID is immutable and does not change when labels are
        /// added or removed, when messages are moved between folders, or when
        /// UIDVALIDITY changes.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public long? GmailThrId
        {
            get {                 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_GmailThrId; 
            }
        }

        #endregion
    }
}
