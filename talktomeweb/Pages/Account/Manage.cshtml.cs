using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SharedLibrary.Services;

namespace talktomeweb.Pages.Account
{
    public class ManageModel : PageModel
    {
        private readonly AuthService _authService;

        public ManageModel(AuthService authService)
        {
            _authService = authService;
        }

        public dynamic CurrentUser { get; private set; }

        public IActionResult OnGet()
        {
            CurrentUser = _authService.GetCurrentlyLoggedInUser();
            if (CurrentUser == null)
            {
                return RedirectToPage("/Index");
            }

            return Page();
        }

        public IActionResult OnPostLogout()
        {
            _authService.Logout();
            return RedirectToPage("/Index");
        }
    }
}
