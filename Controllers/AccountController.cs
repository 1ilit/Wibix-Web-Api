using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using wibix_api.Models;
using wibix_api.Services;

namespace wibix_api.Controllers;

[ApiController]
[Route("[controller]")]
public class AccountController : Controller{
    private readonly UserManager<User> userManager=null!;
    private readonly SignInManager<User> signInManager=null!;
    private readonly IAuthManager authManager=null!;
    public static IWebHostEnvironment env{get; set;}=null!;
    public AccountController(UserManager<User> _userManager, SignInManager<User> _signInManager, IAuthManager _authManager, IWebHostEnvironment _env){
        userManager=_userManager;
        signInManager=_signInManager;
        authManager=_authManager;
        env=_env;
    }

    [HttpGet("Users")]
    public IActionResult GetUsers(){
        List<VisibleInfo> users=new List<VisibleInfo>();
        List<User> list=userManager.Users.OrderBy(u=>u.Rating).ToList();
        list.Reverse();
        foreach (var i in list)
        { 
            users.Add(new VisibleInfo{
                Id=i.Id,
                DisplayName=i.DisplayName,
                UserName=i.UserName,
                Email=i.Email,
                Rating=i.Rating,
                ImageSrc=i.ImageSrc,
                Bio=i.Bio
            });
        }
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id){
        var user= await userManager.FindByIdAsync(id);
        return Ok(user);
    }

    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromBody] UserRegister user){
        if(!ModelState.IsValid)
            return BadRequest();
        
        User u=new User();
        u.Bio="";
        u.UserName=user.UserName;
        u.Email=user.Email;
        u.DisplayName=user.UserName;
        u.Roles=new List<string>(){"User"};

        var results= await userManager.CreateAsync(u, user.Password);
        if(!results.Succeeded){
            foreach(var e in results.Errors){
                ModelState.AddModelError(e.Code, e.Description);
            }
            return BadRequest(ModelState);
        }

        foreach (var r in u.Roles)
        {
            await userManager.AddToRoleAsync(u, r);
        }

        return Accepted(u);
        
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromBody] UserLogin model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest("model not valid");
        }
        try
        {   
            User u=await userManager.FindByNameAsync(model.UserName);

            if(! await authManager.ValidateUser(model, u)){
                return Unauthorized();
            }

            VisibleInfo user=new VisibleInfo{
                Id=u.Id,
                DisplayName=u.DisplayName,
                UserName=u.UserName,
                Email=u.Email,
                Rating=u.Rating,
                ImageSrc=u.ImageSrc,
                Bio=u.Bio
            };

            return Accepted(new {Token=await authManager.CreateToken(u),
            VisibleInfo=user});
            
        }
        catch (Exception ex)
        {  
            return Problem (ex.HelpLink, ex.StackTrace, statusCode: 500);
        }
    }

    [Authorize]
    [HttpPost("UpdateProfile")]
    public async Task<IActionResult> UpdateProfile([FromForm]UpdateProfile model)
    {
        if(model.File!=null)
        {
            string fileName=new String(Path.GetFileNameWithoutExtension(model.File.FileName).Take(10).ToArray()).Replace(' ', '-');
            fileName=fileName+DateTime.Now.ToString("yymmssfff")+Path.GetExtension(model.File.FileName);
            string serverFolder=Path.Combine(env.WebRootPath, "Uploads/", fileName);

            model.File.CopyTo(new FileStream(serverFolder, FileMode.Create));

            var user=await userManager.FindByIdAsync(model.Id);

            user.ImageSrc=fileName;
            user.Bio=model.Bio;
            user.DisplayName=model.DisplayName;
            user.Email=model.Email;

            await userManager.UpdateAsync(user);

            return Ok("updated profile");
        }
        else{
            return BadRequest("file null");
        }
    }

    [Authorize]
    [HttpDelete("DeleteUser/{id}")]
    public async Task<IActionResult> DeleteUser(string id){
        var user=await userManager.FindByIdAsync(id);

        await userManager.DeleteAsync(user);

        return Ok("user deleted");
    }

}