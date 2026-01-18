using System.Collections.Generic;

namespace OCPP.Core.Management.Models.Auth
{
    public class UserListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<UserDto> Users { get; set; }
        public int TotalCount { get; set; }
    }

    public class UserDetailsResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserDto User { get; set; }
        public WalletDto Wallet { get; set; }
        public List<UserVehicleDto> Vehicles { get; set; }
    }

    public class WalletResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public WalletDto Wallet { get; set; }
    }

    public class UserVehicleResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserVehicleDto Vehicle { get; set; }
    }

    public class UserVehicleListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<UserVehicleDto> Vehicles { get; set; }
    }
}
