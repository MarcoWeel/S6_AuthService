using authservice.Helpers;
using authservice.Models;
using authservice.Services;
using Microsoft.AspNetCore.Mvc;

namespace authservice.Controllers
{
    [Route("")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IDataAccessService _dataAccessService;

        public AuthController(IUserService userService, IDataAccessService dataAccessService)
        {
            _userService = userService;
            _dataAccessService = dataAccessService;
        }

        [HttpPost("authenticate")]
        public IActionResult Authenticate(AuthenticateRequest authenticateRequest)
        {
            var response = _userService.Authenticate(authenticateRequest).Result;

            if (!response.Success)
            {
                return NotFound(new ProblemDetails()
                {
                    Title = response.Details
                });
            }

            return Ok(response.Result);
        }

        [HttpPost("register")]
        public IActionResult Register(RegisterRequest registerRequest)
        {
            AuthenticateResponse response = _userService.Register(registerRequest).Result;

            if (!response.Success)
            {
                Dictionary<string, string[]> errors = new();
                errors.Add("Email", new string[] { response.Details });
                ValidationProblemDetails problem = new(errors);
                return BadRequest(problem);
            }

            return Ok(response.Result);
        }

        [Authorize]
        [HttpGet("authenticated")]
        public IActionResult Authenticated()
        {
            User user = (User)HttpContext.Items["User"];
            string token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            AuthenticateResponse response = new(user, token);

            return Ok(response.Result);
        }

        [Authorize]
        [HttpDelete("delete")]
        public IActionResult Delete()
        {
            User user = (User)HttpContext.Items["User"];

            DeleteResponse response = _userService.Delete(user.Id).Result;

            if (!response.Success)
            {
                return NotFound(new ProblemDetails()
                {
                    Title = response.Details
                });
            }

            return Ok(response.Result);
        }

        [Authorize]
        [HttpPut("updateuser")]
        public IActionResult Update(UpdateRequest updateRequest)
        {
            User user = (User)HttpContext.Items["User"];

            if(!(user.Id == updateRequest.Id || user.Roles.HasFlag(Roles.Admin)))
            {
                return Unauthorized(new ProblemDetails()
                {
                    Title = "Not authorised to update this user."
                });
            }

            AuthenticateResponse response = user.Roles.HasFlag(Roles.Admin)?
                _userService.UpdateAsAdmin(updateRequest).Result :
                _userService.Update(updateRequest).Result;

            if (!response.Success)
            {
                return NotFound(new ProblemDetails()
                {
                    Title = response.Details
                });
            }

            return Ok(response.Result);
        }

        [Authorize]
        [HttpGet("users")]
        public IActionResult Users(Guid id)
        {
            User user = (User)HttpContext.Items["User"];
            if (!user.Roles.HasFlag(Roles.Admin))
            {
                return Unauthorized(new ProblemDetails()
                {
                    Title = "Not authorised to get users as: " + user.Roles.ToString() + "."
                });
            }

            List<User> response = _dataAccessService.GetUsers().Result;

            return Ok(response);
        }

        [Authorize]
        [HttpPut("update_roles")]
        public IActionResult VerifyUser(AuthorizeUserRequest authorizeUserRequest)
        {
            User user = (User)HttpContext.Items["User"];
            if (!user.Roles.HasFlag(Roles.Admin))
            {
                return Unauthorized(new ProblemDetails()
                {
                    Title = "Not authorised to update roles."
                });
            }

            return NotFound();
        }
    }
}
