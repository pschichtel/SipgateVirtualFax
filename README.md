# SipgateVirtualFax

A simple tool that uses TWAIN to scan a document, embeds it into a PDF and sends it as a fax using Sipgate's API. Information in Sipgate's fax functionality: https://basicsupport.sipgate.de/hc/de/articles/115000291945-Faxe-versenden-via-PDF-API-Faxdrucker

What works?

- Single page flatbed scanning (using ntwain)
- Converting the resulting image into a PDF (using iTextSharp)
- Pulling available group faxlines from sipgate
- Fax status polling
- Fax existing PDF without scanning
- Offering resend upon send failure
- Initial progress reporting
- Progress indication in the UI

What's still missing?

- multi page scanning using ADF
- multi page scanning using manual swap with UI guidance
- Contact book integration
