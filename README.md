# SipgateVirtualFax

A simple tool that uses TWAIN to scan a document, embeds it into a PDF and sends it as a fax using Sipgate's API. Information in Sipgate's fax functionality: https://basicsupport.sipgate.de/hc/de/articles/115000291945-Faxe-versenden-via-PDF-API-Faxdrucker

What works?

- Single page flatbed scanning (using ntwain)
- Converting the resting image into a PDF (using iTextSharp)
- Pulling available group faxlines from sipgate
- Sending the PDF as a fax

What's still missing?

- multi page scanning using ADF
- multi page scanning using manual swap with UI guidance
- Progress indication in the UI
- Fax status polling
- Offering resend upon send failure
- Fax existing PDF without scanning
- Contact book integration
