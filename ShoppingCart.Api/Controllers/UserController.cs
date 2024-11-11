using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using ShoppingCart.Api.ViewModels;
using ShoppingCart.Common.Contracts;

namespace UTN_Avanzada2_TP_Final.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly IJsonManager<UserVM> _jsonManager;
        private readonly string _jsonFilePath = @"Data/Users.json";
        private IList<UserVM> _userList;

        public UserController(
            IConfiguration configuration,
            IMemoryCache memoryCache,
            IJsonManager<UserVM> jsonManager)
        {
            _configuration = configuration;
            _memoryCache = memoryCache;
            _jsonManager = jsonManager;

            var cacheKey = $"{nameof(UserController)}";

            _userList = _memoryCache.GetOrCreate(cacheKey, entry => {
                var duration = _configuration.GetChildren().Any(x => x.Key.Equals("MemoryCacheDurationInSeconds")) ? _configuration.GetValue<double>("MemoryCacheDurationInSeconds") : 300;

                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(duration);

                return _jsonManager.GetContent(_jsonFilePath) ?? new List<UserVM>();
            });
        }

        [HttpGet]
        [Route("")]
        [AllowAnonymous]
        public IActionResult GetAll()
        {
            return Ok(_userList);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("Login")] 
        public IActionResult Login(LoginCredentialsVM loginCredentials)
        {
            var user = _userList
                .Where(x => x.Email == loginCredentials.Email)
                .FirstOrDefault();
            
            if ((user == null) || (user.Password != loginCredentials.Password))
                return Unauthorized();

            var token = GenerateJWTToken(user);

            return Ok(new 
            {
                token = token,
                userDetails = user
            });
        }

        private string GenerateJWTToken(UserVM user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]));

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var role = (user.UserTypeId == 1) ? "Admin" : "Client";

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}