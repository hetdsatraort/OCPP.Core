using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class Users
    {
        public string RecId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EMailID { get; set; }
        public string PhoneNumber { get; set; }
        public string CountryCode { get; set; }
        public string Password { get; set; }
        public string ProfileImageID { get; set; } //url
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string PinCode { get; set; }
        public string ProfileCompleted { get; set; }
        public string LastLogin { get; set; }
        public string UserRole { get; set; }
        public string CreditBalance { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

    }
}
