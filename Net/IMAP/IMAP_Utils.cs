using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace LumiSoft.Net.IMAP
{
	/// <summary>
	/// Provides utility methods for IMAP.
	/// </summary>
	public class IMAP_Utils
	{	        
        #region static method MessageFlagsAdd

        /// <summary>
        /// Adds specified flags to flags list.
        /// </summary>
        /// <param name="flags">Current message flags.</param>
        /// <param name="flagsToAdd">Flags to add.</param>
        /// <returns>Returns new flags.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>flags</b> or <b>flagsToAdd</b> is null reference.</exception>
        public static string[] MessageFlagsAdd(string[] flags,string[] flagsToAdd)
        {
            if(flags == null){
                throw new ArgumentNullException("flags");
            }
            if(flagsToAdd == null){
                throw new ArgumentNullException("flagsToAdd");
            }

            List<string> retVal = new List<string>();
            retVal.AddRange(flags);

            foreach(string flagToAdd in flagsToAdd){
                bool contains = false;
                foreach(string flag in flags){
                    if(string.Equals(flag,flagToAdd,StringComparison.InvariantCultureIgnoreCase)){
                        contains = true;
                        break;
                    }
                }

                if(!contains){
                    retVal.Add(flagToAdd);
                }
            }

            return retVal.ToArray();
        }

        #endregion

        #region static method MessageFlagsRemove

        /// <summary>
        /// Removes specified flags from message flags list.
        /// </summary>
        /// <param name="flags">Message flags.</param>
        /// <param name="flagsToRemove">Message flags to remove.</param>
        /// <returns>Returns new message flags.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>flags</b> or <b>flagsToRemove</b> is null reference.</exception>
        public static string[] MessageFlagsRemove(string[] flags,string[] flagsToRemove)
        {
            if(flags == null){
                throw new ArgumentNullException("flags");
            }
            if(flagsToRemove == null){
                throw new ArgumentNullException("flagsToRemove");
            }

            List<string> retVal = new List<string>();
            foreach(string flag in flags){
                bool remove = false;
                foreach(string flagToRemove in flagsToRemove){
                    if(string.Equals(flag,flagToRemove,StringComparison.InvariantCultureIgnoreCase)){
                        remove = true;
                        break;
                    }
                }

                if(!remove){
                    retVal.Add(flag);
                }
            }

            return retVal.ToArray();
        }

        #endregion


		#region method ParseDate

		/// <summary>
		/// Parses IMAP date time from string.
		/// </summary>
		/// <param name="date">DateTime string.</param>
		/// <returns>Returns parsed date-time value.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>date</b> is null reference.</exception>
		public static DateTime ParseDate(string date)
		{
            if(date == null){
                throw new ArgumentNullException("date");
            }

            /* RFC 3501. IMAP date format. 
			    date-time       = DQUOTE date-day-fixed "-" date-month "-" date-year SP time SP zone DQUOTE
				date            = day-month-year
                date-day-fixed  = (SP DIGIT) / 2DIGIT
                                ; Fixed-format version of date-day
                date-month      = "Jan" / "Feb" / "Mar" / "Apr" / "May" / "Jun" /
                                  "Jul" / "Aug" / "Sep" / "Oct" / "Nov" / "Dec"
				time            = 2DIGIT ":" 2DIGIT ":" 2DIGIT
			*/
            if(date.IndexOf('-') > -1){
                try{
                    return DateTime.ParseExact(date.Trim(),new string[]{"d-MMM-yyyy","d-MMM-yyyy HH:mm:ss zzz"},System.Globalization.DateTimeFormatInfo.InvariantInfo,System.Globalization.DateTimeStyles.None);
                }
                catch{
                    throw new ArgumentException("Argument 'date' value '" + date + "' is not valid IMAP date.");
                }
            }
            else{
                return LumiSoft.Net.MIME.MIME_Utils.ParseRfc2822DateTime(date);
            }
		}

		#endregion

		#region static DateTimeToString

		/// <summary>
		/// Converts date time to IMAP date time string.
		/// </summary>
		/// <param name="date">DateTime to convert.</param>
		/// <returns></returns>
		public static string DateTimeToString(DateTime date)
		{			
			string retVal = "";
			retVal += date.ToString("dd-MMM-yyyy HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
			retVal += " " + date.ToString("zzz",System.Globalization.CultureInfo.InvariantCulture).Replace(":","");

			return retVal;
		}

		#endregion


        #region static method Encode_IMAP_UTF7_String

		/// <summary>
		/// Encodes specified data with IMAP modified UTF7 encoding. Defined in RFC 3501 5.1.3.  Mailbox International Naming Convention.
		/// Example: öö is encoded to &amp;APYA9g-.
		/// </summary>
		/// <param name="text">Text to encode.</param>
		/// <returns></returns>
		public static string Encode_IMAP_UTF7_String(string text)
		{
			/* RFC 3501 5.1.3.  Mailbox International Naming Convention
				In modified UTF-7, printable US-ASCII characters, except for "&",
				represent themselves; that is, characters with octet values 0x20-0x25
				and 0x27-0x7e.  The character "&" (0x26) is represented by the
				two-octet sequence "&-".

				All other characters (octet values 0x00-0x1f and 0x7f-0xff) are
				represented in modified BASE64, with a further modification from
				[UTF-7] that "," is used instead of "/".  Modified BASE64 MUST NOT be
				used to represent any printing US-ASCII character which can represent
				itself.
				
				"&" is used to shift to modified BASE64 and "-" to shift back to
				US-ASCII.  There is no implicit shift from BASE64 to US-ASCII, and
				null shifts ("-&" while in BASE64; note that "&-" while in US-ASCII
				means "&") are not permitted.  However, all names start in US-ASCII,
				and MUST end in US-ASCII; that is, a name that ends with a non-ASCII
				ISO-10646 character MUST end with a "-").
			*/

			// Base64 chars, except '/' is replaced with ','
			char[] base64Chars = new char[]{
				'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
				'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
				'0','1','2','3','4','5','6','7','8','9','+',','
			};

			MemoryStream retVal = new MemoryStream();
			for(int i=0;i<text.Length;i++){
				char c = text[i];

				// The character "&" (0x26) is represented by the two-octet sequence "&-".
				if(c == '&'){
					retVal.Write(new byte[]{(byte)'&',(byte)'-'},0,2);
				}
				// It is allowed char, don't need to encode
				else if(c >= 0x20 && c <= 0x25 || c >= 0x27 && c <= 0x7E){
					retVal.WriteByte((byte)c);
				}
				// Not allowed char, encode it
				else{
					// Superfluous shifts are not allowed. 
					// For example: öö may not encoded as &APY-&APY-, but must be &APYA9g-.

					// Get all continuous chars that need encoding and encode them as one block
					MemoryStream encodeBlock = new MemoryStream();
					for(int ic=i;ic<text.Length;ic++){
						char cC = text[ic];

						// Allowed char
						if(cC >= 0x20 && cC <= 0x25 || cC >= 0x27 && cC <= 0x7E){
							break;
						}
						else{
							encodeBlock.WriteByte((byte)((cC & 0xFF00) >> 8));
							encodeBlock.WriteByte((byte)(cC & 0xFF));
							i = ic;
						}
					}

					// Ecode block
					byte[] encodedData = Net_Utils.Base64EncodeEx(encodeBlock.ToArray(),base64Chars,false);
					retVal.WriteByte((byte)'&');
					retVal.Write(encodedData,0,encodedData.Length);
					retVal.WriteByte((byte)'-');
				}
			}

			return System.Text.Encoding.Default.GetString(retVal.ToArray());
		}

		#endregion

		#region static method Decode_IMAP_UTF7_String

		/// <summary>
		/// Decodes IMAP modified UTF7 encoded data. Defined in RFC 3501 5.1.3.  Mailbox International Naming Convention.
		/// Example: &amp;APYA9g- is decoded to öö.
		/// </summary>
		/// <param name="text">Text to encode.</param>
		/// <returns></returns>
		public static string Decode_IMAP_UTF7_String(string text)
		{
			/* RFC 3501 5.1.3.  Mailbox International Naming Convention
				In modified UTF-7, printable US-ASCII characters, except for "&",
				represent themselves; that is, characters with octet values 0x20-0x25
				and 0x27-0x7e.  The character "&" (0x26) is represented by the
				two-octet sequence "&-".

				All other characters (octet values 0x00-0x1f and 0x7f-0xff) are
				represented in modified BASE64, with a further modification from
				[UTF-7] that "," is used instead of "/".  Modified BASE64 MUST NOT be
				used to represent any printing US-ASCII character which can represent
				itself.
				
				"&" is used to shift to modified BASE64 and "-" to shift back to
				US-ASCII.  There is no implicit shift from BASE64 to US-ASCII, and
				null shifts ("-&" while in BASE64; note that "&-" while in US-ASCII
				means "&") are not permitted.  However, all names start in US-ASCII,
				and MUST end in US-ASCII; that is, a name that ends with a non-ASCII
				ISO-10646 character MUST end with a "-").
			*/

            // Base64 chars, except '/' is replaced with ','
			char[] base64Chars = new char[]{
				'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
				'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
				'0','1','2','3','4','5','6','7','8','9','+',','
			};

			StringBuilder retVal = new StringBuilder();
			for(int i=0;i<text.Length;i++){
				char c = text[i];

				// Encoded block or escaped &
				if(c == '&'){
					int endingPos = -1;
					// Read encoded block
					for(int b=i+1;b<text.Length;b++){
						// - marks block end
						if(text[b] == '-'){
							endingPos = b;
							break;
						}
						// Invalid & sequence, just treat it as '&' char and not like shift.
						// &....&, but must be &....-
						else if(text[b] == '&'){							
							break;
						}
					}
			
					// If no ending -, invalid encoded block. Treat it like it is
					if(endingPos == -1){
						// Just let main for to handle other chars after &
						retVal.Append(c);
					}
					// If empty block, then escaped &
					else if(endingPos - i == 1){
						retVal.Append(c);
						// Move i over '-'
						i++;
					}
					// Decode block
					else{
						// Get encoded block
						byte[] encodedBlock = System.Text.Encoding.Default.GetBytes(text.Substring(i + 1,endingPos - i - 1));
		
						// Convert to UTF-16 char						
						byte[] decodedData = Net_Utils.Base64DecodeEx(encodedBlock,base64Chars);
						char[] decodedChars = new char[decodedData.Length / 2];                        
						for(int iC=0;iC<decodedChars.Length;iC++){
							decodedChars[iC] = (char)(decodedData[iC * 2] << 8 | decodedData[(iC * 2) + 1]);
						}
                        
						// Decode data
						retVal.Append(decodedChars);

						// Move i over '-'
						i += encodedBlock.Length + 1;
					}
				}
				// Normal byte
				else{
					retVal.Append(c);
				}
			}

			return retVal.ToString();
		}

		#endregion

        #region static method EncodeMailbox

        /// <summary>
        /// Encodes mailbox name.
        /// </summary>
        /// <param name="mailbox">Mailbox name.</param>
        /// <param name="encoding">Mailbox name encoding mechanism.</param>
        /// <returns>Renturns quoted encoded mailbox name.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>mailbox</b> is null reference.</exception>
        public static string EncodeMailbox(string mailbox,IMAP_Mailbox_Encoding encoding)
        {
            if(mailbox == null){
                throw new ArgumentNullException("mailbox");
            }

            /* RFC 6855 3.
                quoted        = DQUOTE *uQUOTED-CHAR DQUOTE
                uQUOTED-CHAR  = QUOTED-CHAR / UTF8-2 / UTF8-3 / UTF8-4
            */

            string encoded;
			if (encoding == IMAP_Mailbox_Encoding.ImapUtf7){
				encoded = IMAP_Utils.Encode_IMAP_UTF7_String(mailbox);
			}
			else if (encoding == IMAP_Mailbox_Encoding.ImapUtf8){
				encoded = mailbox;
			}
			else{
				encoded = mailbox;
			}

			// Escape internal quotes inside the mailbox name
			encoded = encoded.Replace("\"", "\\\"");

			// Wrap in IMAP quoted string
			return "\"" + encoded + "\"";
        }

        #endregion

        #region static method DecodeMailbox

        /// <summary>
        /// Decodes mailbox name.
        /// </summary>
        /// <param name="mailbox">Mailbox name.</param>
        /// <returns>Returns decoded mailbox name.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>mailbox</b> is null reference.</exception>
        public static string DecodeMailbox(string mailbox)
        {
            if(mailbox == null){
                throw new ArgumentNullException("mailbox");
            }

            /* RFC 5738 3.
                string        =/ utf8-quoted
                utf8-quoted   = "*" DQUOTE *UQUOTED-CHAR DQUOTE
                UQUOTED-CHAR  = QUOTED-CHAR / UTF8-2 / UTF8-3 / UTF8-4
            */

            // UTF-8 mailbox name.
            if(mailbox.StartsWith("*\"")){
                return mailbox.Substring(2,mailbox.Length - 3);
            }
            else{
                return Decode_IMAP_UTF7_String(TextUtils.UnQuoteString(mailbox));
            }
        }

        #endregion


        #region static method NormalizeFolder

        /// <summary>
		/// Normalizes folder path.  Example: /Inbox/SubFolder/ will be Inbox/SubFolder.
		/// </summary>
		/// <param name="folder">Folder path to normalize.</param>
		/// <returns>Returns normalized folder path.</returns>
		public static string NormalizeFolder(string folder)
		{
			folder = folder.Replace("\\","/");
			if(folder.StartsWith("/")){
				folder = folder.Substring(1);
			}
			if(folder.EndsWith("/")){
				folder = folder.Substring(0,folder.Length - 1);
			}

			return folder;
		}

		#endregion

        #region static method IsValidFolderName

        /// <summary>
        /// Gets if the specified folder name is valid folder name.
        /// </summary>
        /// <param name="folder">Folder name.</param>
        /// <returns>Returns true if specified folde name is valid.</returns>
        public static bool IsValidFolderName(string folder)
        {
            // TODO: Path ?

            return true;
        }

        #endregion


        #region static method MustUseLiteralString

        /// <summary>
		/// Determines whether the given IMAP string must be encoded as a literal
		/// instead of a quoted string, according to RFC 3501 and RFC 6855 rules.
		/// </summary>
		/// <param name="value">
		/// The string to evaluate. A null value never requires a literal.
		/// </param>
		/// <param name="utf8">
		/// Indicates whether UTF‑8 quoted strings are allowed (UTF8=USER or UTF8=ACCEPT).
		/// </param>
		/// <returns>
		/// True if the string contains characters that cannot appear in ASCII or
		/// UTF‑8 quoted strings; otherwise false.
		/// </returns>
		/// <remarks>
		/// A literal is required when the value contains non‑ASCII characters while
		/// UTF‑8 is unavailable, a double quote, a backslash when UTF‑8 quoting is used,
		/// or any control character such as CR, LF, or NUL.
		/// </remarks>
        public static bool MustUseLiteralString(string? value,bool utf8)
		{
			if(value == null){
				return false;
			}

			/*
			   RFC 6855 — UTF‑8 Quoted Strings and IMAP Literals

			   IMAP string values may be transmitted using one of three encodings:

				   1. Quoted string (ASCII)
				   2. UTF‑8 quoted string (RFC 6855)
				   3. Literal (RFC 3501)

			   -----------------------------------------------------------------------
			   1. ASCII Quoted String
			   -----------------------------------------------------------------------
				   quoted-string = DQUOTE *QUOTED-CHAR DQUOTE

				   QUOTED-CHAR = %x01-7F except DQUOTE / "\" / CR / LF

				   Used when:
					 - All characters are 7‑bit ASCII
					 - No control characters
					 - No DQUOTE, backslash, CR, or LF

			   -----------------------------------------------------------------------
			   2. UTF‑8 Quoted String (UTF8=USER or UTF8=ACCEPT)
			   -----------------------------------------------------------------------
				   utf8-quoted = DQUOTE *UQUOTED-CHAR DQUOTE

				   UQUOTED-CHAR = UTF8-CHAR except DQUOTE / "\" / CR / LF

				   Notes:
					 - UTF‑8 quoted strings MUST NOT contain DQUOTE, "\" , CR, or LF.
					 - UTF‑8 quoted strings MUST be used for userid when UTF8=USER
					   capability is advertised, unless forbidden characters appear.

				   A UTF‑8 quoted string is simply:
					   "päev"
					   "Jüri"
					   "用户"

				   Forbidden examples (must use literal instead):
					   "pa\"ev"   ; contains DQUOTE
					   "pa\ev"    ; contains backslash
					   "pa\n"     ; contains LF
					   "pa\r"     ; contains CR
			*/

			foreach (char c in value){
				// UTF‑8 not supported → any non‑ASCII forces literal
				if(!utf8 && c > 0x7F){
					return true;
				}

				// DQUOTE not allowed in quoted strings
				if(c == '"'){
					return true;
				}

				// DQUOTE not allowed in quoted strings
				if(utf8 && c == '\\'){
					return true;
				}

				// Control characters (CR, LF, NUL, etc.) force literal
				if(char.IsControl(c)){
					return true;
				}
			}

			return false;
		}


        #endregion

        #region static method IsValidLiteralHeader

        /// <summary>
        /// Determines whether the specified string is a valid IMAP literal or literal+ header.
        /// </summary>
        /// <param name="header">Literal header string, e.g. "{12}" or "{12+}".</param>
        /// <returns>
        /// True if the header is a syntactically valid literal or literal+ header; otherwise false.
        /// </returns>
        public static bool IsValidLiteralHeader(string header)
		{
			if (string.IsNullOrEmpty(header)){
				return false;
			}

			// Must be at least "{0}" → length >= 3
			if (header.Length < 3 || header[0] != '{' || header[^1] != '}'){
				return false;
			}

			// Extract inner part: "12" or "12+"
			string inner = header.Substring(1, header.Length - 2);


			// Check for literal+
			if (inner.EndsWith("+", StringComparison.Ordinal)){
				inner = inner.Substring(0, inner.Length - 1);
			}

			// Must be digits only
			if (!int.TryParse(inner, out int count)){
				return false;
			}

			// RFC: literal count must be >= 0
			if (count < 0){
				return false;
			}

			return true;
		}

		#endregion



        #region static method ReadString

        /// <summary>
        /// Reads IMAP string-literal/string/astring/nstring/utf8-quoted from string reader.
        /// </summary>
        /// <param name="reader">String reader.</param>
        /// <returns>Returns IMAP string.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>reader</b> is null reference.</exception>
        public static string? ReadString(StringReader reader)
        {
            if(reader == null){
                throw new ArgumentNullException("reader");
            }

            reader.ReadToFirstChar();

            // We have string-literal.
            if(reader.SourceString.StartsWith("{")){
                int literalSize = Convert.ToInt32(reader.ReadParenthesized());
                // Literal has CRLF ending, skip it.
                reader.ReadSpecifiedLength(2);
                                                
                return reader.ReadSpecifiedLength(literalSize);
            }
            // utf8-quoted old rfc 5738
            else if(reader.StartsWith("*\"")){
                reader.ReadSpecifiedLength(1);

                return reader.ReadWord();
            }
            // string/astring/nstring
            else{
                // Accept chars '<' '>' in token.
                string? word = reader.ReadWord(true,new char[]{' ',',',';','{','}','(',')','[',']','\r','\n'},false);
                
                // nstring
                if(string.Equals(word,"NIL",StringComparison.InvariantCultureIgnoreCase)){
                    return null;
                }

                return word;
            }
        }

        #endregion

    }
}
