# LumiSoft.Net

A cross‑platform .NET networking library providing **client and server components** for multiple Internet protocols: **SMTP, POP3, IMAP, FTP**, plus **DNS, MIME, WebDav, STUN, UPnP**.

---

## Features

### Mail Protocols
- **SMTP Client (STARTTLS, AUTH PLAIN/LOGIN/CRAM-MD5/DIGEST-MD5/NTLM/XOAUTH/XOAUTH2, SIZE, DSN, BINARYMIME, CHUNKING, ENHANCEDSTATUSCODES, Socks5, http-connect)**
- **POP3 Client (STLS, SASL PLAIN/LOGIN/CRAM-MD5/DIGEST-MD5/NTLM/XOAUTH/XOAUTH2, APOP, TOP, USER, UIDL, RESP-CODES, UTF8,Socks5, http-connect)**
- **IMAP Client (STARTTLS, SASL PLAIN/LOGIN/CRAM-MD5/DIGEST-MD5/NTLM/XOAUTH/XOAUTH2, IDLE, LITERAL+, UIDPLUS, Gmail extensions, Socks5, http-connect)**
- **SMTP Server**
- **POP3 Server**
- **IMAP Server**
- **MIME** message creation and parsing

### Networking Components
- **DNS Client**
- **FTP Client & Server**
- **WebDav Client**
- **UPnP Client**
- **STUN Client**

---

## Version History

### 10.2.0 (25.09.2026)

#### IMAP Client
- Complete rewrite with fully modern async/await implementation  
- Added Socks5 and http-connect proxy support  
- Added usage example, see Examples link below

#### POP3 Client
- Fix Connect and ConnectAsync ssl connect.
- Remove maxCount from POP3_ClientMessage methods. Replaced by automatic server reported 
  message size + 1MB safety limit. 
- Added usage example, see Examples link below

### 10.1.0 (09.09.2026)

#### SMTP Client
- Complete rewrite with fully modern async/await implementation  
- Added Socks5 and http-connect proxy support  
- Improved STARTTLS handling  
- Updated SASL authentication pipeline  
- Better error handling and session state management  

#### POP3 Client
- Complete rewrite with fully modern async/await implementation  
- Added Socks5 and http-connect proxy support  
- Improved TOP, UIDL, and partial RETR handling  
- Full MIME integration including partial MIME parsing 

---

## Examples

[https://github.com/ivx675/LumiSoft.Net/tree/master/Examples](https://github.com/ivx675/LumiSoft.Net/tree/master/Examples)

## Repository

[https://github.com/ivx675/LumiSoft.Net](https://github.com/ivx675/LumiSoft.Net)


## Installation

Example:

```bash
dotnet add package LumiSoft.Net.dll
