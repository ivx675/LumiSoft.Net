using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Provides MIME-related utility methods.
    /// </summary>
    public class MIME_Utils
    {
        #region static method DateTimeToRfc2822

		/// <summary>
        /// Converts the specified date and time to an RFC 2822 date-time
        /// string.
        /// </summary>
        /// <param name="dateTime">
        /// Date and time value to convert.
        /// </param>
        /// <returns>
        /// Returns the RFC 2822 date-time string representation of
        /// <paramref name="dateTime"/>.
        /// </returns>
		public static string DateTimeToRfc2822(DateTime dateTime)
		{            
            return dateTime.ToString("ddd, dd MMM yyyy HH':'mm':'ss ",System.Globalization.DateTimeFormatInfo.InvariantInfo) + dateTime.ToString("zzz").Replace(":","");
		}

		#endregion

        #region static method ParseRfc2822DateTime

        /// <summary>
        /// Parses an RFC 2822 date-time value.
        /// </summary>
        /// <param name="value">
        /// RFC 2822 date-time string to parse.
        /// </param>
        /// <returns>
        /// Returns the parsed date and time value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="value"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="value"/> is not a valid RFC 822 or
        /// RFC 2822 date-time value.
        /// </exception>
        public static DateTime ParseRfc2822DateTime(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            /* RFC 2822 3.
             *      date-time       =       [ day-of-week "," ] date FWS time [CFWS]
             *      day-of-week     =       ([FWS] day-name) / obs-day-of-week
             *      day-name        =       "Mon" / "Tue" / "Wed" / "Thu" / "Fri" / "Sat" / "Sun"
             *      date            =       day month year 
             *      year            =       4*DIGIT / obs-year
             *      month           =       (FWS month-name FWS) / obs-month
             *      month-name      =       "Jan" / "Feb" / "Mar" / "Apr" / "May" / "Jun" / "Jul" / "Aug" / "Sep" / "Oct" / "Nov" / "Dec"
             *      day             =       ([FWS] 1*2DIGIT) / obs-day
             *      time            =       time-of-day FWS zone
             *      time-of-day     =       hour ":" minute [ ":" second ]
             *      hour            =       2DIGIT / obs-hour
             *      minute          =       2DIGIT / obs-minute
             *      second          =       2DIGIT / obs-second
             *      zone            =       (( "+" / "-" ) 4DIGIT) / obs-zone
             * 
             *      The date and time-of-day SHOULD express local time.
            */

            try{
                MIME_Reader r = new MIME_Reader(value);
                string? v = r.Atom();
                if(v == null){
                    throw new ArgumentException("Invalid RFC 2822.3 date-time value '" + value + "'.");
                }
                // Skip optional [ day-of-week "," ] and read "day".
                if (v.Length == 3){
                    r.Char(true);
                    v = r.Atom();
                }
                int day = Convert.ToInt32(v);
                // Read month
                v = r.Atom();
                if(v == null){
                    throw new ArgumentException("Invalid RFC 2822.3 date-time value '" + value + "'.");
                }
                int month = 1;
                if(string.Equals(v,"jan",StringComparison.OrdinalIgnoreCase)){
                    month = 1;
                }
                else if(string.Equals(v,"feb",StringComparison.OrdinalIgnoreCase)){
                    month = 2;
                }
                else if(string.Equals(v,"mar",StringComparison.OrdinalIgnoreCase)){
                    month = 3;
                }
                else if(string.Equals(v,"apr",StringComparison.OrdinalIgnoreCase)){
                    month = 4;
                }
                else if(string.Equals(v,"may",StringComparison.OrdinalIgnoreCase)){
                    month = 5;
                }
                else if(string.Equals(v,"jun",StringComparison.OrdinalIgnoreCase)){
                    month = 6;
                }
                else if(string.Equals(v,"jul",StringComparison.OrdinalIgnoreCase)){
                    month = 7;
                }
                else if(string.Equals(v,"aug",StringComparison.OrdinalIgnoreCase)){
                    month = 8;
                }
                else if(string.Equals(v,"sep",StringComparison.OrdinalIgnoreCase)){
                    month = 9;
                }
                else if(string.Equals(v,"oct",StringComparison.OrdinalIgnoreCase)){
                    month = 10;
                }
                else if(string.Equals(v,"nov",StringComparison.OrdinalIgnoreCase)){
                    month = 11;
                }
                else if(string.Equals(v,"dec",StringComparison.OrdinalIgnoreCase)){
                    month = 12;
                }
                else{
                    throw new ArgumentException("Invalid RFC 2822.3 month-name value '" + value + "'.");
                }
                int year   = Convert.ToInt32(r.Atom());
                int hour   = Convert.ToInt32(r.Atom());
                r.Char(true);
                int minute = Convert.ToInt32(r.Atom());
                int second = 0;
                // We have optional "second".
                if(r.Peek(true) == ':'){
                    r.Char(true);
                    second = Convert.ToInt32(r.Atom());
                }
                int timeZoneMinutes = 0;
                v = r.Atom();
                // Time zone missing. Not RFC syntax, but some servers will send such dates.
                if(v == null){
                    // Just consider time zone as 0(GMT).
                }
                // We have RFC 2822 date. For example: +2000.
                else if(v[0] == '+' || v[0] == '-'){
                    if(v[0] == '+'){
                        timeZoneMinutes = (Convert.ToInt32(v.Substring(1,2)) * 60 + Convert.ToInt32(v.Substring(3,2)));
                    }
                    else{
                        timeZoneMinutes = -(Convert.ToInt32(v.Substring(1,2)) * 60 + Convert.ToInt32(v.Substring(3,2)));
                    }
                }
                // We have RFC 822 date with abbrevated time zone name. For example: GMT.
                else{
                    v = v.ToUpper();

                    #region time zones

                    // Alpha Time Zone (military).
                    if(v == "A"){
                        timeZoneMinutes = ((01 * 60) + 00);
                    }
                    // Australian Central Daylight Time.
                    else if(v == "ACDT"){
                        timeZoneMinutes = ((10 * 60) + 30);
                    }
                    // Australian Central Standard Time.
                    else if(v == "ACST"){
                        timeZoneMinutes = ((09 * 60) + 30);
                    }
                    // Atlantic Daylight Time.
                    else if(v == "ADT"){
                        timeZoneMinutes = -((03 * 60) + 00);
                    }
                    // Australian Eastern Daylight Time.
                    else if(v == "AEDT"){
                        timeZoneMinutes = ((11 * 60) + 00);
                    }
                    // Australian Eastern Standard Time.
                    else if(v == "AEST"){
                        timeZoneMinutes = ((10 * 60) + 00);
                    }
                    // Alaska Daylight Time.
                    else if(v == "AKDT"){
                        timeZoneMinutes = -((08 * 60) + 00);
                    }
                    // Alaska Standard Time.
                    else if(v == "AKST"){
                        timeZoneMinutes = -((09 * 60) + 00);
                    }
                    // Atlantic Standard Time.
                    else if(v == "AST"){
                        timeZoneMinutes = -((04 * 60) + 00);
                    }
                    // Australian Western Daylight Time.
                    else if(v == "AWDT"){
                        timeZoneMinutes = ((09 * 60) + 00);
                    }
                    // Australian Western Standard Time.
                    else if(v == "AWST"){
                        timeZoneMinutes = ((08 * 60) + 00);
                    }
                    // Bravo Time Zone (millitary).
                    else if(v == "B"){
                        timeZoneMinutes = ((02 * 60) + 00);
                    }
                    // British Summer Time.
                    else if(v == "BST"){
                        timeZoneMinutes = ((01 * 60) + 00);
                    }
                    // Charlie Time Zone (millitary).
                    else if(v == "C"){
                        timeZoneMinutes = ((03 * 60) + 00);
                    }
                    // Central Daylight Time.
                    else if(v == "CDT"){
                        timeZoneMinutes = -((05 * 60) + 00);
                    }
                    // Central European Daylight Time.
                    else if(v == "CEDT"){
                        timeZoneMinutes = ((02 * 60) + 00);
                    }
                    // Central European Summer Time.
                    else if(v == "CEST"){
                        timeZoneMinutes = ((02 * 60) + 00);
                    }
                    // Central European Time.
                    else if(v == "CET"){
                        timeZoneMinutes = ((01 * 60) + 00);
                    }
                    // Central Standard Time.
                    else if(v == "CST"){
                        timeZoneMinutes = -((06 * 60) + 00);
                    }
                    // Christmas Island Time.
                    else if(v == "CXT"){
                        timeZoneMinutes = ((01 * 60) + 00);
                    }
                    // Delta Time Zone (military).
                    else if(v == "D"){
                        timeZoneMinutes = ((04 * 60) + 00);
                    }
                    // Echo Time Zone (military).
                    else if(v == "E"){
                        timeZoneMinutes = ((05 * 60) + 00);
                    }
                    // Eastern Daylight Time.
                    else if(v == "EDT"){
                        timeZoneMinutes = -((04 * 60) + 00);
                    }
                    // Eastern European Daylight Time.
                    else if(v == "EEDT"){
                        timeZoneMinutes = ((03 * 60) + 00);
                    }
                    // Eastern European Summer Time.
                    else if(v == "EEST"){
                        timeZoneMinutes = ((03 * 60) + 00);
                    }
                    // Eastern European Time.
                    else if(v == "EET"){
                        timeZoneMinutes = ((02 * 60) + 00);
                    }
                    // Eastern Standard Time.
                    else if(v == "EST"){
                        timeZoneMinutes = -((05 * 60) + 00);
                    }
                    // Foxtrot Time Zone (military).
                    else if(v == "F"){
                        timeZoneMinutes = (06 * 60 + 00);
                    }
                    // Golf Time Zone (military).
                    else if(v == "G"){
                        timeZoneMinutes = ((07 * 60) + 00);
                    }
                    // Greenwich Mean Time.
                    else if(v == "GMT"){
                        timeZoneMinutes = 0000;
                    }
                    // Hotel Time Zone (military).
                    else if(v == "H"){
                        timeZoneMinutes = ((08 * 60) + 00);
                    }
                    // India Time Zone (military).
                    else if(v == "I"){
                        timeZoneMinutes = ((09 * 60) + 00);
                    }
                    // Irish Summer Time.
                    else if(v == "IST"){
                        timeZoneMinutes = ((01 * 60) + 00);
                    }
                    // Kilo Time Zone (millitary).
                    else if(v == "K"){
                        timeZoneMinutes = ((10 * 60) + 00);
                    }
                    // Lima Time Zone (millitary).
                    else if(v == "L"){
                        timeZoneMinutes = ((11 * 60) + 00);
                    }
                    // Mike Time Zone (millitary).
                    else if(v == "M"){
                        timeZoneMinutes = ((12 * 60) + 00);
                    }
                    // Mountain Daylight Time.
                    else if(v == "MDT"){
                        timeZoneMinutes = -((06 * 60) + 00);
                    }
                    // Mountain Standard Time.
                    else if(v == "MST"){
                        timeZoneMinutes = -((07 * 60) + 00);
                    }
                    // November Time Zone (military).
                    else if(v == "N"){
                        timeZoneMinutes = -((01 * 60) + 00);
                    }
                    // Newfoundland Daylight Time.
                    else if(v == "NDT"){
                        timeZoneMinutes = -((02 * 60) + 30);
                    }
                    // Norfolk (Island) Time.
                    else if(v == "NFT"){
                        timeZoneMinutes = ((11 * 60) + 30);
                    }
                    // Newfoundland Standard Time.
                    else if(v == "NST"){
                        timeZoneMinutes = -((03 * 60) + 30);
                    }
                    // Oscar Time Zone (military).
                    else if(v == "O"){
                        timeZoneMinutes = -((02 * 60) + 00);
                    }
                    // Papa Time Zone (military).
                    else if(v == "P"){
                        timeZoneMinutes = -((03 * 60) + 00);
                    }
                    // Pacific Daylight Time.
                    else if(v == "PDT"){
                        timeZoneMinutes = -((07 * 60) + 00);
                    }
                    // Pacific Standard Time.
                    else if(v == "PST"){
                        timeZoneMinutes = -((08 * 60) + 00);
                    }
                    // Quebec Time Zone (military).
                    else if(v == "Q"){
                        timeZoneMinutes = -((04 * 60) + 00);
                    }
                    // Romeo Time Zone (military).
                    else if(v == "R"){
                        timeZoneMinutes = -((05 * 60) + 00);
                    }
                    // Sierra Time Zone (military).
                    else if(v == "S"){
                        timeZoneMinutes = -((06 * 60) + 00);
                    } 
                    // Tango Time Zone (military).
                    else if(v == "T"){
                        timeZoneMinutes = -((07 * 60) + 00);
                    }
                    // Uniform Time Zone (military).
                    else if(v == "U"){
                        timeZoneMinutes = -((08 * 60) + 00);
                    }
                    // Coordinated Universal Time.
                    else if(v == "UTC"){
                        timeZoneMinutes = 0000;
                    }
                    // Victor Time Zone (militray).
                    else if(v == "V"){
                        timeZoneMinutes = -((09 * 60) + 00);
                    }
                    // Whiskey Time Zone (military).
                    else if(v == "W"){
                        timeZoneMinutes = -((10 * 60) + 00);
                    }
                    // Western European Daylight Time.
                    else if(v == "WEDT"){
                        timeZoneMinutes = ((01 * 60) + 00);
                    }
                    // Western European Summer Time.
                    else if(v == "WEST"){
                        timeZoneMinutes = ((01 * 60) + 00);
                    }
                    // Western European Time.
                    else if(v == "WET"){
                        timeZoneMinutes = 0000;
                    }
                    // Western Standard Time.
                    else if(v == "WST"){
                        timeZoneMinutes = ((08 * 60) + 00);
                    }
                    // X-ray Time Zone (military).
                    else if(v == "X"){
                        timeZoneMinutes = -((11 * 60) + 00);
                    }
                    // Yankee Time Zone (military).
                    else if(v == "Y"){
                        timeZoneMinutes = -((12 * 60) + 00);
                    }
                    // Zulu Time Zone (military).
                    else if(v == "Z"){
                        timeZoneMinutes = 0000;
                    }

                    #endregion
                }
                        
                // Convert time to UTC and then back to local.
                DateTime timeUTC = new DateTime(year,month,day,hour,minute,second).AddMinutes(-(timeZoneMinutes));
                return new DateTime(timeUTC.Year,timeUTC.Month,timeUTC.Day,timeUTC.Hour,timeUTC.Minute,timeUTC.Second,DateTimeKind.Utc).ToLocalTime();
            }
            catch(Exception x){
                string dymmy = x.Message;
                throw new ArgumentException($"'{value}' is not a valid RFC 822 or RFC 2822 date-time value.",nameof(value));
            }
        }

        #endregion


        #region static method UnfoldHeader

        /// <summary>
        /// Unfolds a folded header field.
        /// </summary>
        /// <param name="value">
        /// Header field value to unfold.
        /// </param>
        /// <returns>
        /// Returns the unfolded header field value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="value"/> is
        /// <see langword="null"/>.
        /// </exception>
        public static string UnfoldHeader(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            /* RFC 2822 2.2.3 Long Header Fields.
				The process of moving from this folded multiple-line representation
				of a header field to its single line representation is called
				"unfolding". Unfolding is accomplished by simply removing any CRLF
				that is immediately followed by WSP.
            */

            return value.Replace("\r\n","");
        }

        #endregion


        #region static method CreateMessageID

		/// <summary>
        /// Creates an RFC 2822 message identifier.
        /// </summary>
        /// <returns>
        /// Returns a message identifier in the form
        /// <c>&lt;id-left@id-right&gt;</c>.
        /// </returns>
		public static string CreateMessageID()
		{
            return $"<{Guid.NewGuid():N}@{Guid.NewGuid():N}>";
        }

		#endregion


        #region static method QDecode

		/// <summary>
        /// Decodes RFC 2047 Q-encoded text.
        /// </summary>
        /// <param name="encoding">
        /// Character set used to decode the text.
        /// </param>
        /// <param name="data">
        /// Q-encoded text to decode.
        /// </param>
        /// <returns>
        /// Returns the decoded text.
        /// </returns>
        /// <remarks>
        /// Q encoding is similar to quoted-printable encoding except that
        /// underscore characters (<c>_</c>) are decoded as spaces.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="encoding"/> or
        /// <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
		public static string QDecode(Encoding encoding,string data)
		{
            if(encoding == null){
                throw new ArgumentNullException(nameof(encoding));
            }
            if(data == null){
                throw new ArgumentNullException(nameof(data));
            }

			return encoding.GetString(QuotedPrintableDecode(Encoding.ASCII.GetBytes(data.Replace("_"," "))));
		}

		#endregion

        #region static method QuotedPrintableDecode

		/// <summary>
        /// Decodes quoted-printable encoded data as defined in RFC 2045
        /// section 6.7.
        /// </summary>
        /// <param name="data">
        /// Quoted-printable encoded data to decode.
        /// </param>
        /// <returns>
        /// Returns the decoded data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="data"/> is
        /// <see langword="null"/>.
        /// </exception>
		public static byte[] QuotedPrintableDecode(byte[] data)
		{
            if(data == null){
                throw new ArgumentNullException(nameof(data));
            }

			/* RFC 2045 6.7. Quoted-Printable Content-Transfer-Encoding
			 
				(1)	(General 8bit representation) Any octet, except a CR or
					LF that is part of a CRLF line break of the canonical
					(standard) form of the data being encoded, may be
					represented by an "=" followed by a two digit
					hexadecimal representation of the octet's value.  The
					digits of the hexadecimal alphabet, for this purpose,
					are "0123456789ABCDEF".  Uppercase letters must be
					used; lowercase letters are not allowed.

				(2) (Literal representation) Octets with decimal values of
					33 through 60 inclusive, and 62 through 126, inclusive,
					MAY be represented as the US-ASCII characters which
					correspond to those octets (EXCLAMATION POINT through
					LESS THAN, and GREATER THAN through TILDE, respectively).
					
				(3) (White Space) Octets with values of 9 and 32 MAY be
					represented as US-ASCII TAB (HT) and SPACE characters,
					respectively, but MUST NOT be so represented at the end
					of an encoded line.  Any TAB (HT) or SPACE characters
					on an encoded line MUST thus be followed on that line
					by a printable character.  In particular, an "=" at the
					end of an encoded line, indicating a soft line break
					(see rule #5) may follow one or more TAB (HT) or SPACE
					characters.  It follows that an octet with decimal
					value 9 or 32 appearing at the end of an encoded line
					must be represented according to Rule #1.  This rule is
					necessary because some MTAs (Message Transport Agents,
					programs which transport messages from one user to
					another, or perform a portion of such transfers) are
					known to pad lines of text with SPACEs, and others are
					known to remove "white space" characters from the end
					of a line.  Therefore, when decoding a Quoted-Printable
					body, any trailing white space on a line must be
					deleted, as it will necessarily have been added by
					intermediate transport agents.
					
				(4) (Line Breaks) A line break in a text body, represented
				    as a CRLF sequence in the text canonical form, must be
					represented by a (RFC 822) line break, which is also a
					CRLF sequence, in the Quoted-Printable encoding.  Since
					the canonical representation of media types other than
					text do not generally include the representation of
					line breaks as CRLF sequences, no hard line breaks
					(i.e. line breaks that are intended to be meaningful
					and to be displayed to the user) can occur in the
					quoted-printable encoding of such types.  Sequences
					like "=0D", "=0A", "=0A=0D" and "=0D=0A" will routinely
					appear in non-text data represented in quoted-
					printable, of course.

				(5) (Soft Line Breaks) The Quoted-Printable encoding
					REQUIRES that encoded lines be no more than 76
					characters long.  If longer lines are to be encoded
					with the Quoted-Printable encoding, "soft" line breaks
			*/

			MemoryStream msRetVal = new MemoryStream();
			MemoryStream msSourceStream = new MemoryStream(data);

			int b = msSourceStream.ReadByte();
			while(b > -1){
				// Encoded 8-bit byte(=XX) or soft line break(=CRLF)
				if(b == '='){
					byte[] buffer = new byte[2];
					int nCount = msSourceStream.Read(buffer,0,2);
					if(nCount == 2){
						// Soft line break, line splitted, just skip CRLF
						if(buffer[0] == '\r' && buffer[1] == '\n'){
						}
						// This must be encoded 8-bit byte
						else{
							try{
								msRetVal.Write(Net_Utils.FromHex(buffer),0,1);
							}
							catch{
								// Illegal value after =, just leave it as is
								msRetVal.WriteByte((byte)'=');
								msRetVal.Write(buffer,0,2);
							}
						}
					}
					// Illegal =, just leave as it is
					else{
						msRetVal.WriteByte((byte)'=');
					}
				}
				// Just write back all other bytes
				else{
					msRetVal.WriteByte((byte)b);
				}

				// Read next byte
				b = msSourceStream.ReadByte();
			}

			return msRetVal.ToArray();
		}

		#endregion
    }
}
