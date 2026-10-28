using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the abstract base class for all IMAP SEARCH key types
    /// defined by RFC 3501.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IMAP SEARCH keys define the criteria used to select messages within a
    /// mailbox. Each concrete subclass implements a specific search operator,
    /// such as <c>FROM</c>, <c>TO</c>, <c>TEXT</c>, <c>UID</c>, logical
    /// operators like <c>OR</c> and <c>NOT</c>, or structural constructs such
    /// as parenthesized groups.
    /// </para>
    /// </remarks>
    public abstract class IMAP_t_Search_Key
    {
        #region static method ParseKeyAsync

        /// <summary>
        /// Parses one search key or search key group.
        /// </summary>
        /// <param name="r">IMAP reader.</param>
        /// <returns>Returns one parsed search key or search key group.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>r</b> is null reference.</exception>
        /// <exception cref="ParseException">Is raised when parsing fails.</exception>
        internal static async Task<IMAP_t_Search_Key> ParseKeyAsync(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }
           
            // Keys group
            if(r.PeekIs("(")){
                return await IMAP_t_Search_Key_Group.ParseAsync(r);
            }                
            // ALL
            else if(r.PeekIs("ALL")){
                return IMAP_t_Search_Key_All.Parse(r);
            }
            // ANSWERED
            else if(r.PeekIs("ANSWERED")){
                return IMAP_t_Search_Key_Answered.Parse(r);
            }
            // BCC
            else if(r.PeekIs("BCC")){
                return await IMAP_t_Search_Key_Bcc.ParseAsync(r);
            }
            // BEFORE
            else if(r.PeekIs("BEFORE")){
                return IMAP_t_Search_Key_Before.Parse(r);
            }
            // BODY
            else if(r.PeekIs("BODY")){
                return await IMAP_t_Search_Key_Body.ParseAsync(r);
            }
            // CC
            else if(r.PeekIs("CC")){
                return await IMAP_t_Search_Key_Cc.ParseAsync(r);
            }
            // DELETED
            else if(r.PeekIs("DELETED")){
                return IMAP_t_Search_Key_Deleted.Parse(r);
            }
            // DRAFT
            else if(r.PeekIs("DRAFT")){
                return IMAP_t_Search_Key_Draft.Parse(r);
            }
            // FLAGGED
            else if(r.PeekIs("FLAGGED")){
                return IMAP_t_Search_Key_Flagged.Parse(r);
            }
            // FROM
            else if(r.PeekIs("FROM")){
                return await IMAP_t_Search_Key_From.ParseAsync(r);
            }
            // HEADER
            else if(r.PeekIs("HEADER")){
                return await IMAP_t_Search_Key_Header.ParseAsync(r);
            }
            // KEYWORD
            else if(r.PeekIs("KEYWORD")){
                return IMAP_t_Search_Key_Keyword.Parse(r);
            }
            // LARGER
            else if(r.PeekIs("LARGER")){
                return IMAP_t_Search_Key_Larger.Parse(r);
            }
            // NEW
            else if(r.PeekIs("NEW")){
                return IMAP_t_Search_Key_New.Parse(r);
            }
            // NOT
            else if(r.PeekIs("NOT")){
                return await IMAP_t_Search_Key_Not.ParseAsync(r);
            }
            // OLD
            else if(r.PeekIs("OLD")){
                return IMAP_t_Search_Key_Old.Parse(r);
            }
            // ON
            else if(r.PeekIs("ON")){
                return IMAP_t_Search_Key_On.Parse(r);
            }
            // OR
            else if(r.PeekIs("OR")){
                return await IMAP_t_Search_Key_Or.ParseAsync(r);
            }
            // RECENT
            else if(r.PeekIs("RECENT")){
                return IMAP_t_Search_Key_Recent.Parse(r);
            }
            // SEEN
            else if(r.PeekIs("SEEN")){
                return IMAP_t_Search_Key_Seen.Parse(r);
            }
            // SENTBEFORE
            else if(r.PeekIs("SENTBEFORE")){
                return IMAP_t_Search_Key_SentBefore.Parse(r);
            }
            // SENTON
            else if(r.PeekIs("SENTON")){
                return IMAP_t_Search_Key_SentOn.Parse(r);
            }
            // SENTSINCE
            else if(r.PeekIs("SENTSINCE")){
                return IMAP_t_Search_Key_SentSince.Parse(r);
            }
            // SEQSET
            else if(r.PeekIs("SEQSET")){
                return IMAP_t_Search_Key_SeqSet.Parse(r);
            }
            // SINCE
            else if(r.PeekIs("SINCE")){
                return IMAP_t_Search_Key_Since.Parse(r);
            }
            // SMALLER
            else if(r.PeekIs("SMALLER")){
                return IMAP_t_Search_Key_Smaller.Parse(r);
            }
            // SUBJECT
            else if(r.PeekIs("SUBJECT")){
                return await IMAP_t_Search_Key_Subject.ParseAsync(r);
            }
            // TEXT
            else if(r.PeekIs("TEXT")){
                return await IMAP_t_Search_Key_Text.ParseAsync(r);
            }
            // TO
            else if(r.PeekIs("TO")){
                return await IMAP_t_Search_Key_To.ParseAsync(r);
            }
            // UID
            else if(r.PeekIs("UID")){
                return IMAP_t_Search_Key_Uid.Parse(r);
            }
            // UNANSWERED
            else if(r.PeekIs("UNANSWERED")){
                return IMAP_t_Search_Key_Unanswered.Parse(r);
            }
            // UNDELETED
            else if(r.PeekIs("UNDELETED")){
                return IMAP_t_Search_Key_Undeleted.Parse(r);
            }
            // UNDRAFT
            else if(r.PeekIs("UNDRAFT")){
                return IMAP_t_Search_Key_Undraft.Parse(r);
            }
            // UNFLAGGED
            else if(r.PeekIs("UNFLAGGED")){
                return IMAP_t_Search_Key_Unflagged.Parse(r);
            }
            // UNKEYWORD
            else if(r.PeekIs("UNKEYWORD")){
                return IMAP_t_Search_Key_Unkeyword.Parse(r);
            }
            // UNSEEN
            else if(r.PeekIs("UNSEEN")){
                return IMAP_t_Search_Key_Unseen.Parse(r);
            }
            else{
                // Check if we have sequence-set. Because of IMAP specification sucks a little here, why the hell they didn't 
                // do the keyword(SEQSET) for it, like UID. Now we just have to try if it is sequence-set or BAD key.
                try{
                   return IMAP_t_Search_Key_SeqSet.Parse(r);
                }
                catch{
                   throw new ParseException("Not supported search key.");
                }
            }
        }

        #endregion


        #region method ToCommandBuilder

        /// <summary>
        /// Stores search-key to search command builder.
        /// </summary>
        /// <param name="builder">Search command builder.</param>
        internal abstract void ToCommandBuilder(CommandBuilder builder);

        #endregion
    }
}
