using SharedLibrary.Models;
using SharedLibrary.Data;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SharedLibrary.Interface;
using System.Text.RegularExpressions;


namespace SharedLibrary.Services
{
    public class UserService
    {
        private readonly UserData _userData;
        private readonly IUserContext _userContext;
        private List<User> _users;
        public User? CurrentlyLoggedInUser { get; private set; }

        public UserService(IServiceConfig config)
        {
            string connectionString = config.ConnectionString;
            _userData = new UserData(connectionString);
            _users = new List<User>();
            _userContext = config.UserContext; // This replaces direct IHttpContextAccessor usage

            LoadAllDataAsync().Wait();
            CheckAndSetCurrentlyLoggedInUser();
        }


        //* Managing Data

        private void CheckAndSetCurrentlyLoggedInUser()
        {
            var email = _userContext.GetCurrentUserEmail();
            if (!string.IsNullOrEmpty(email))
            {
                var user = _users.FirstOrDefault(u => u.Email == email);
                if (user != null)
                {
                    CurrentlyLoggedInUser = user;
                }
            }
        }


        private async Task LoadAllDataAsync()
        {
            var usersFromData = await _userData.GetAllUsers();
            foreach (var userData in usersFromData)
            {
                dynamic userObject = JsonConvert.DeserializeObject(userData);

                string userType = userObject.UserType;
                if (userType.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    var adminUser = JsonConvert.DeserializeObject<Admin>(userData);
                    _users.Add(adminUser);
                }
                else if (userType.Equals("Client", StringComparison.OrdinalIgnoreCase))
                {
                    var clientUser = JsonConvert.DeserializeObject<Client>(userData);
                    _users.Add(clientUser);
                }
            }
        }


        //* Crud Methods

        public async Task<bool> RegisterUserAsync(string username, string email, string imagePath, string password, DateTime registrationDate, string userType, string bio = null, int? status = null, int? permission = null)
        {
            //! Regular expression pattern for email validation
            string emailPattern = @"^\w+([\.-]?\w+)*@\w+([\.-]?\w+)*(\.\w{2,3})+$";

            if (!Regex.IsMatch(email, emailPattern))
            {
                throw new ArgumentException("Invalid email format.");
            }
            if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) || u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A user with the same username or email already exists.");
            }
            try
            {
                byte[] salt = GenerateSalt();

                string passwordHash = HashPassword(password, salt);

                User newUser;
                if (userType.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    newUser = new Admin
                    {
                        Username = username,
                        Email = email,
                        ImagePath = imagePath,
                        PasswordHash = passwordHash,
                        Salt = Convert.ToBase64String(salt),
                        RegistrationDate = registrationDate,
                        Permission = permission.HasValue ? (Permission)permission.Value : Permission.Basic
                    };
                }
                else if (userType.Equals("Client", StringComparison.OrdinalIgnoreCase))
                {
                    newUser = new Client
                    {
                        Username = username,
                        Email = email,
                        ImagePath = imagePath,
                        PasswordHash = passwordHash,
                        Salt = Convert.ToBase64String(salt),
                        RegistrationDate = registrationDate,
                        Bio = bio,
                        Status = status.HasValue ? (Status)status.Value : Status.Active // Default to Active if null
                    };
                }
                else
                {
                    throw new ArgumentException("User type must be either 'Admin' or 'Client'.");
                }

                _users.Add(newUser);
                await _userData.AddUser(username, email, imagePath, passwordHash, Convert.ToBase64String(salt), registrationDate, userType, bio, status, permission);
                _userContext.SetCurrentUserEmail(email);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }


        public bool LoginUser(string email, string password)
        {
            var user = _users.FirstOrDefault(u => u.Email == email);

            if (user != null && !(user is Admin)) // Check if user is not an admin
            {
                string enteredPasswordHash = HashPassword(password, Convert.FromBase64String(user.Salt));

                if (enteredPasswordHash == user.PasswordHash)
                {
                    CurrentlyLoggedInUser = user;
                    _userContext.SetCurrentUserEmail(email);
                    return true;
                }
            }

            return false;
        }


        public bool LoginAdmin(string email, string password)
        {
            var user = _users.FirstOrDefault(u => u.Email == email);

            if (user != null && user is Admin admin)
            {
                string enteredPasswordHash = HashPassword(password, Convert.FromBase64String(admin.Salt));

                if (enteredPasswordHash == admin.PasswordHash)
                {
                    CurrentlyLoggedInUser = admin;
                    _userContext.SetCurrentUserEmail(email);
                    return true;
                }
            }

            return false;
        }

        public void Logout()
        {
            CurrentlyLoggedInUser = null;

            _userContext.ClearCurrentUserEmail();
        }



        public async Task EditUser(int userId, string username, string email, string imagePath, string? password, DateTime registrationDate, string userType, string bio = null, int? status = null, int? permission = null)
        {

            //! Regular expression pattern for email validation
            string emailPattern = @"^\w+([\.-]?\w+)*@\w+([\.-]?\w+)*(\.\w{2,3})+$";

            if (!Regex.IsMatch(email, emailPattern))
            {
                throw new ArgumentException("Invalid email format.");
            }
            var user = _users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                throw new Exception("User not found.");
            }

            if (_users.Any(u => (u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) || u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) && u.UserId != userId))
            {
                throw new InvalidOperationException("Another user with the same username or email already exists.");
            }

            bool changesDetected = false;

            if (user.Username != username) { user.Username = username; changesDetected = true; }
            if (user.Email != email) { user.Email = email; changesDetected = true; }
            if (user.ImagePath != imagePath) { user.ImagePath = imagePath; changesDetected = true; }
            if (user.RegistrationDate != registrationDate) { user.RegistrationDate = registrationDate; changesDetected = true; }

            if (user is Admin admin && permission.HasValue && admin.Permission != (Permission)permission.Value)
            {
                admin.Permission = (Permission)permission.Value;
                changesDetected = true;
            }
            else if (user is Client client)
            {
                if (bio != null && client.Bio != bio) { client.Bio = bio; changesDetected = true; }
                if (status.HasValue && client.Status != (Status)status.Value) { client.Status = (Status)status.Value; changesDetected = true; }
            }

            if (password != null)
            {
                byte[] salt = GenerateSalt();
                string newPasswordHash = HashPassword(password, salt);
                if (user.PasswordHash != newPasswordHash)
                {
                    user.PasswordHash = newPasswordHash;
                    user.Salt = Convert.ToBase64String(salt);
                    changesDetected = true;
                }
            }

            if (!changesDetected)
            {
                throw new Exception("No changes detected.");
            }

            await _userData.UpdateUser(userId, username, email, imagePath, user.PasswordHash, user.Salt, registrationDate, userType, bio, status, permission);
        }


        public async Task DeleteUser(int userId)
        {
            var user = _users.FirstOrDefault(u => u.UserId == userId);
            if (user != null)
            {
                _users.Remove(user);
                await _userData.DeleteUser(userId);
            }
            else
            {
                throw new Exception("User not found.");
            }
        }

        public List<dynamic> SearchUsers(string query)
        {
            return _users
                .Where(user => user.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(user => (dynamic)user)
                .ToList();
        }


        public dynamic? GetCurrentlyLoggedInUser()
        {
            if (CurrentlyLoggedInUser is Admin)
            {
                return CurrentlyLoggedInUser as Admin;
            }
            else if (CurrentlyLoggedInUser is Client)
            {
                return CurrentlyLoggedInUser as Client;
            }
            else
            {
                return null;
            }
        }

        public dynamic? GetUserById(int userId)
        {
            var user = _users.FirstOrDefault(u => u.UserId == userId);

            if (user != null)
            {
                if (user is Admin)
                {
                    return (Admin)user;
                }
                else if (user is Client)
                {
                    return (Client)user;
                }
            }

            return null;
        }

        public int GetTotalUsers()
        {
            return _users.Count;
        }

        public int GetUsersCreatedToday()
        {
            return _users.Count(u => u.RegistrationDate.Date == DateTime.Today);
        }

        public List<Admin> GetAllAdmins()
        {
            return _users.OfType<Admin>().ToList();
        }




        //* Helper Methods

        // Method to generate random salt
        private byte[] GenerateSalt()
        {
            // Create an array of bytes to store the salt
            byte[] salt = new byte[16];

            // Use RNGCryptoServiceProvider to generate random bytes for the salt
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt); // Fill the salt array with random bytes
            }

            return salt; // Return the generated salt
        }

        // Method to hash password with salt
        private string HashPassword(string password, byte[] salt)
        {
            // Create an instance of Rfc2898DeriveBytes to hash the password using PBKDF2 algorithm
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000))
            {
                byte[] hash = pbkdf2.GetBytes(20); // secret message that's 20 characters long
                byte[] hashBytes = new byte[36]; // Create an array to store the combined salt and hash bytes

                // Copy the salt bytes to the beginning of the hashBytes array
                Array.Copy(salt, 0, hashBytes, 0, 16);

                // Copy the hash bytes to the end of the hashBytes array
                Array.Copy(hash, 0, hashBytes, 16, 20);

                // Convert the combined salt and hash bytes to a base64-encoded string
                return Convert.ToBase64String(hashBytes);
            }
        }

        public static implicit operator UserService(UserServiceTest v)
        {
            throw new NotImplementedException();
        }
    }

    public class UserServiceTest
    {
        private readonly Dictionary<int, User> _userDataStore;
        private readonly IUserContext _userContext;
        private List<User> _users;
        private int _nextUserId;
        public User? CurrentlyLoggedInUser { get; private set; }

        public UserServiceTest(IServiceConfig config, Dictionary<int, User> userDataStore)
        {
            _users = new List<User>();
            _userDataStore = userDataStore;
            _nextUserId = _userDataStore.Any() ? _userDataStore.Keys.Max() + 1 : 1;
            _userContext = config.UserContext; // This replaces direct IHttpContextAccessor usage

            LoadAllDataAsync().Wait();
            CheckAndSetCurrentlyLoggedInUser();
        }


        //* Managing Data

        private void CheckAndSetCurrentlyLoggedInUser()
        {
            var email = _userContext.GetCurrentUserEmail();
            if (!string.IsNullOrEmpty(email))
            {
                var user = _users.FirstOrDefault(u => u.Email == email);
                if (user != null)
                {
                    CurrentlyLoggedInUser = user;
                }
            }
        }


        public async Task LoadAllDataAsync()
        {
            var usersFromData = _userDataStore.Values.ToList();
            foreach (var userData in usersFromData)
            {

                if (userData is Admin)
                {
                    _users.Add(userData);
                }
                else if (userData is Client)
                {
                    _users.Add(userData);
                }
            }
        }


        //* Crud Methods

        public async Task<bool> RegisterUserAsync(string username, string email, string imagePath, string password, DateTime registrationDate, string userType, string bio = null, int? status = null, int? permission = null)
        {
            try
            {
                byte[] salt = GenerateSalt();

                string passwordHash = HashPassword(password, salt);

                User newUser;
                if (userType.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    newUser = new Admin
                    {
                        Username = username,
                        Email = email,
                        ImagePath = imagePath,
                        PasswordHash = passwordHash,
                        Salt = Convert.ToBase64String(salt),
                        RegistrationDate = registrationDate,
                        Permission = permission.HasValue ? (Permission)permission.Value : Permission.Basic
                    };
                }
                else if (userType.Equals("Client", StringComparison.OrdinalIgnoreCase))
                {
                    newUser = new Client
                    {
                        Username = username,
                        Email = email,
                        ImagePath = imagePath,
                        PasswordHash = passwordHash,
                        Salt = Convert.ToBase64String(salt),
                        RegistrationDate = registrationDate,
                        Bio = bio,
                        Status = status.HasValue ? (Status)status.Value : Status.Active // Default to Active if null
                    };
                }
                else
                {
                    throw new ArgumentException("User type must be either 'Admin' or 'Client'.");
                }

                _users.Add(newUser);
                _userDataStore.Add(_nextUserId, newUser);
                _nextUserId++;
                _userContext.SetCurrentUserEmail(email);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }


        public bool LoginUser(string email, string password)
        {
            var user = _users.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                string enteredPasswordHash = HashPassword(password, Convert.FromBase64String(user.Salt));

                if (enteredPasswordHash == user.PasswordHash)
                {
                    CurrentlyLoggedInUser = user;
                    _userContext.SetCurrentUserEmail(email);
                    return true;
                }
            }

            return false;
        }

        public bool LoginAdmin(string email, string password)
        {
            var user = _users.FirstOrDefault(u => u.Email == email);

            if (user != null && user is Admin admin)
            {
                string enteredPasswordHash = HashPassword(password, Convert.FromBase64String(admin.Salt));

                if (enteredPasswordHash == admin.PasswordHash)
                {
                    CurrentlyLoggedInUser = admin;
                    _userContext.SetCurrentUserEmail(email);
                    return true;
                }
            }

            return false;
        }

        public void Logout()
        {
            CurrentlyLoggedInUser = null;

            _userContext.ClearCurrentUserEmail();
        }



        public async Task EditUser(int userId, string username, string email, string imagePath, string? password, DateTime registrationDate, string userType, string bio = null, int? status = null, int? permission = null)
        {
            if (_userDataStore.TryGetValue(userId, out User user))
            {
                if (password != null)
                {
                    byte[] salt = GenerateSalt();
                    string passwordHash = HashPassword(password, salt);
                    user.PasswordHash = passwordHash;
                    user.Salt = Convert.ToBase64String(salt);
                }

                user.Username = username;
                user.Email = email;
                user.ImagePath = imagePath;
                user.RegistrationDate = registrationDate;

                if (user is Admin admin)
                {
                    admin.Permission = permission.HasValue ? (Permission)permission.Value : admin.Permission;
                }
                else if (user is Client client)
                {
                    client.Bio = bio;
                    client.Status = status.HasValue ? (Status)status.Value : client.Status;
                }

                // Simulate an asynchronous operation
                await Task.CompletedTask;
            }
            else
            {
                throw new Exception("User not found.");
            }
        }



        public async Task DeleteUser(int userId)
        {
            if (_userDataStore.ContainsKey(userId))
            {
                _userDataStore.Remove(userId);
                await Task.CompletedTask;
            }
            else
            {
                throw new Exception("User not found.");
            }
        }


        public List<dynamic> SearchUsers(string query)
        {
            return _users
                .Where(user => user.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(user => (dynamic)user)
                .ToList();
        }


        public dynamic? GetCurrentlyLoggedInUser()
        {
            if (CurrentlyLoggedInUser is Admin)
            {
                return CurrentlyLoggedInUser as Admin;
            }
            else if (CurrentlyLoggedInUser is Client)
            {
                return CurrentlyLoggedInUser as Client;
            }
            else
            {
                return null;
            }
        }

        public dynamic? GetUserById(int userId)
        {
            var user = _users.FirstOrDefault(u => u.UserId == userId);

            if (user != null)
            {
                if (user is Admin)
                {
                    return (Admin)user;
                }
                else if (user is Client)
                {
                    return (Client)user;
                }
            }

            return null;
        }

        public int GetTotalUsers()
        {
            return _users.Count;
        }

        public int GetUsersCreatedToday()
        {
            return _users.Count(u => u.RegistrationDate.Date == DateTime.Today);
        }

        public List<Admin> GetAllAdmins()
        {
            return _users.OfType<Admin>().ToList();
        }

        //* Helper Methods

        // Method to generate random salt
        private byte[] GenerateSalt()
        {
            // Create an array of bytes to store the salt
            byte[] salt = new byte[16];

            // Use RNGCryptoServiceProvider to generate random bytes for the salt
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt); // Fill the salt array with random bytes
            }

            return salt; // Return the generated salt
        }

        // Method to hash password with salt
        public string HashPassword(string password, byte[] salt)
        {
            // Create an instance of Rfc2898DeriveBytes to hash the password using PBKDF2 algorithm
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000))
            {
                byte[] hash = pbkdf2.GetBytes(20); // secret message that's 20 characters long
                byte[] hashBytes = new byte[36]; // Create an array to store the combined salt and hash bytes

                // Copy the salt bytes to the beginning of the hashBytes array
                Array.Copy(salt, 0, hashBytes, 0, 16);

                // Copy the hash bytes to the end of the hashBytes array
                Array.Copy(hash, 0, hashBytes, 16, 20);

                // Convert the combined salt and hash bytes to a base64-encoded string
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
