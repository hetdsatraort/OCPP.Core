using System;

namespace OCPP.Core.Management.Models.Auth
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserDto User { get; set; }
    }

    public class UserDto
    {
        public string RecId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EMailID { get; set; }
        public string PhoneNumber { get; set; }
        public string CountryCode { get; set; }
        public string ProfileImageID { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string PinCode { get; set; }
        public string ProfileCompleted { get; set; }
        public string UserRole { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
