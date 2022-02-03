using System.Globalization;
using System.Windows.Controls;
using PhoneNumbers;

namespace SipGateVirtualFaxGui;

public class PhoneNumberValidation : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value is not string phoneNumber)
        {
            return new ValidationResult(false, "String expected!");
        }

        var phoneNumberUtil = PhoneNumberUtil.GetInstance();
        try
        {
            phoneNumberUtil.Parse(phoneNumber, "DE");
            return ValidationResult.ValidResult;
        }
        catch (NumberParseException e)
        {
            return new ValidationResult(false, $"Not a valid phone number! {e.Message}");
        }
    }
}