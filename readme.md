# LumiSoft.Net

A cross‑platform .NET networking library providing **client and server components** for multiple Internet protocols: **SMTP, POP3, IMAP, FTP**, plus **DNS, MIME, WebDav, STUN, UPnP**.

---

## Features

### Mail Protocols
- **SMTP Client (STARTTLS, AUTH, SIZE, DSN, BINARYMIME, CHUNKING, ENHANCEDSTATUSCODES, Socks5, http-connect)**
- **POP3 Client (STLS, UIDL, SASL, TOP, USER, RESP-CODES, Socks5, http-connect)**
- **IMAP Client**
- **SMTP Server**
- **POP3 Server**
- **IMAP Server**
- **MIME** message creation and parsing

### Networking Components
- **DNS Client**
- **FTP Client & Server**
- **WebDav Client**
- **UPnP Client**
- **STUN** support

---

## Version History

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

## Installation

Example:

```bash
dotnet add package LumiSoft.Net.dll
