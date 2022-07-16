using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VMBackuperBeckEnd.Configuration;
using VMBackuperBeckEnd.Models;
using VMBackuperBeckEnd.Models.DTOs.Requests;
using VMBackuperBeckEnd.Models.DTOs.Responses;
using Newtonsoft.Json;
using VMBackuper.Models.DTOs.Responses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using VMBackuper.Models.DTOs.Requests;
using VMBackuper.Services;
using System.IdentityModel.Tokens.Jwt;

namespace VMBackuperBeckEnd.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<UserAccount> _userManager;
        private readonly SignInManager<UserAccount> _signInManager;
        private readonly JwtConfig _jwtConfig;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthController(UserManager<UserAccount> userManager,
            IOptionsMonitor<JwtConfig> jwtConfig,
            IHttpContextAccessor httpContextAccessor,
            SignInManager<UserAccount> signInManager)
        {
            _userManager = userManager;
            _jwtConfig = jwtConfig.CurrentValue;
            _httpContextAccessor = httpContextAccessor;
            _signInManager = signInManager;
        }

        [HttpGet("GetAllUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = _userManager.Users.ToList();
            return Ok(users);
        }

        [AllowAnonymous]
        //[DisableCors]
        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            Debug.WriteLine($"RefreshToken: {refreshToken}");
            Console.WriteLine($"RefreshToken: {refreshToken}");
            var user = _userManager.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == refreshToken));
            if (user == null)
                return Ok(new AuthResult() { Success = false, Errors = new List<string>() { "user == null", $"refreshToken: {refreshToken}" } });
            var token = user.RefreshTokens.Single(x => x.Token == refreshToken);

            if (!token.IsActive)
                return Ok(new AuthResult() { Success = false, Errors = new List<string>() { "!token.IsActive" } });

            // replace old refresh token with a new one and save
            var newRefreshToken = UserService.GenerateRefreshToken();
            token.Revoked = DateTime.UtcNow;
            token.ReplacedByToken = newRefreshToken.Token;
            //user.RefreshTokens.Clear();
            setRefreshTokenCookie(newRefreshToken.Token);
            user.RefreshTokens.Add(newRefreshToken);
            await _userManager.UpdateAsync(user);
            var jwtToken = UserService.GenerateJwtToken(user);
            return Ok(new AuthResult() { AccessToken = jwtToken });
        }

        [HttpPost]
        [Route("PasswordChange")]
        public async Task<IActionResult> PasswordChange([FromBody] AuthChangePasswordDTO user)
        {
            var existingUser = await _userManager.FindByNameAsync(user.Username);

            if (existingUser == null)
            {
                return BadRequest(new AuthLoginResponseDTO()
                {
                    Errors = new List<string>() { "InvalidUsername" },
                    Success = false,
                });
            }

            var isCorrect = await _userManager.CheckPasswordAsync(existingUser, user.OldPassword);
            if (isCorrect)
            {
                await _userManager.ChangePasswordAsync(existingUser, user.OldPassword, user.NewPassword);
                await _userManager.UpdateAsync(existingUser);
            }
            else
            {
                return BadRequest(new AuthLoginResponseDTO()
                {
                    Errors = new List<string>() { "InvalidCurrentPassword" },
                    Success = false,
                });
            }

            return Ok(new AuthResult() { Success = true });
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] AuthRegistrationDTO user)
        {
            // POST: api/AccountController
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Username);

                if (existingUser != null)
                {
                    return BadRequest(new AuthRegistrationResponseDTO()
                    {
                        Errors = new List<string>() { "emailAlreadyInUse" },
                        Success = false
                    });
                }

                var newUser = new UserAccount() { UserName = user.Username };
                var isCreated = await _userManager.CreateAsync(newUser, user.Password);
                if (isCreated.Succeeded)
                {
                    var jwtToken = UserService.GenerateJwtToken(newUser);
                    var refreshToken = UserService.GenerateRefreshToken();
                    setRefreshTokenCookie(refreshToken.Token);
                    newUser.RefreshTokens.Add(refreshToken);
                    await _userManager.UpdateAsync(newUser);

                    return Ok(new AuthRegistrationResponseDTO()
                    {
                        Success = true,
                        AccessToken = jwtToken,
                        UserAccount = newUser
                    });
                }
                else
                {
                    return BadRequest(new AuthRegistrationResponseDTO()
                    {
                        Errors = isCreated.Errors.Select(x => x.Description).ToList(),
                        Success = false
                    });
                }
            }

            return BadRequest(new AuthRegistrationResponseDTO()
            {
                Errors = new List<string>() { "InvalidPayload" },
                Success = false
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("Logout")]
        public async Task<IActionResult> LogOut()
        {
            var user = await _userManager.GetUserAsync(User);
            Debug.WriteLine("LOGOUT_USER..." + JsonConvert.SerializeObject(user));
            Console.WriteLine("LOGOUT_USER..." + JsonConvert.SerializeObject(user));

            if (user != null)
            {
                //user.RefreshTokens.Clear();
                await _userManager.UpdateAsync(user);
                await _signInManager.SignOutAsync();
            }

            return Ok(new AuthLogoutResponseDTO()
            {
                Success = true,
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] AuthLoginRequestDTO user)
        {
            // POST: api/AccountController
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByNameAsync(user.Username);

                if (existingUser == null)
                {
                    return BadRequest(new AuthLoginResponseDTO()
                    {
                        Errors = new List<string>() { "InvalidLoginRequestUsername" },
                        Success = false,
                    });
                }

                var isCorrect = await _userManager.CheckPasswordAsync(existingUser, user.Password);

                if (!isCorrect)
                {
                    return BadRequest(new AuthLoginResponseDTO()
                    {
                        Errors = new List<string>() { "InvalidLoginRequestPassword" },
                        Success = false
                    });
                }

                //existingUser.RefreshTokens.Clear();
                var jwtToken = UserService.GenerateJwtToken(existingUser);
                var refreshToken = UserService.GenerateRefreshToken();
                setRefreshTokenCookie(refreshToken.Token);
                existingUser.RefreshTokens.Add(refreshToken);
                await _userManager.UpdateAsync(existingUser);

                return Ok(new AuthLoginResponseDTO()
                {
                    Success = true,
                    AccessToken = jwtToken,
                    UserAccount = existingUser
                });
            }

            return BadRequest(new AuthLoginResponseDTO()
            {
                Errors = new List<string>() { "InvalidPayload" },
                Success = false
            });
        }

        // DELETE:  api/AccountController
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            Debug.WriteLine($"DeleteUser: {id}");

            var existingUser = await _userManager.FindByIdAsync(id);

            if (existingUser == null)
            {
                return BadRequest(new AuthLoginResponseDTO()
                {
                    Errors = new List<string>() { "InvalidUsername" },
                    Success = false,
                });
            }
            await _userManager.UpdateSecurityStampAsync(existingUser);
            await _userManager.DeleteAsync(existingUser);

            await _signInManager.SignOutAsync();

            return Ok();
        }

        private void setRefreshTokenCookie(string token)
        {
            string path = _httpContextAccessor.HttpContext.Request.Path.Value;
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = AppConfig.RefreshTokenLifeTime,
                Path = "/api/Auth/RefreshToken"
                //Path = path
                //SameSite = SameSiteMode.None,
                //IsEssential = true,
                //Secure = true
            };
            Response.Cookies.Append("refreshToken", token, cookieOptions);
        }
    }
}