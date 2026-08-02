using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CleanChannel.Landing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CleanChannel.Landing.Pages.Account;

public sealed class LoginModel(DemoLicenseService licenses) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = licenses.ValidateCredentials(Input.Email, Input.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "The email or password was not recognized.");
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Email),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToPage("/Account/Index");
    }

    public sealed class LoginInput
    {
        [Required, EmailAddress, StringLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(128, MinimumLength = 12)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
