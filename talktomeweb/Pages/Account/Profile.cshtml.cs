using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SharedLibrary.Models;
using SharedLibrary.Services;

namespace talktomeweb.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly UserService _userService;
        private readonly AuthService _authService;
        private readonly PostService _postService;
        private readonly FlaggedUserService _flagUserService;
        public List<SharedLibrary.Models.Post> Posts { get; set; } = new List<SharedLibrary.Models.Post>();
        public dynamic SelectedUser { get; private set; }
        public dynamic CurrentUser { get; private set; }
        public FlagUser FlagUser { get; private set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            public string Reason { get; set; }
            public int ToUserId { get; set; }
        }

        public ProfileModel(UserService userService, AuthService authService, PostService postService, FlaggedUserService flaggedUserService)
        {
            _userService = userService;
            _authService = authService;
            _postService = postService;
            _flagUserService = flaggedUserService;
        }

        public IActionResult OnGet(int userId)
        {
            SelectedUser = _userService.GetUserById(userId);
            CurrentUser = _authService.GetCurrentlyLoggedInUser();
            FlagUser = _flagUserService.GetFlagUserByUserId(userId);

            if (CurrentUser == null)
            {
                return RedirectToPage("/Index");
            }

            if (SelectedUser != null)
            {
                Posts = SelectedUser.Posts ?? new List<SharedLibrary.Models.Post>();
                Input = new InputModel
                {
                    Reason = "",
                    ToUserId = SelectedUser.UserId
                };
                return Page();
            }

            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnPostFlagUserAsync()
        {
            try
            {
                int fromUserId = _authService.GetCurrentlyLoggedInUser()?.UserId ?? 0;
                SelectedUser = _userService.GetUserById(Input.ToUserId);
                if (SelectedUser == null)
                {
                    return RedirectToPage("/Index");
                }

                Posts = SelectedUser.Posts ?? new List<SharedLibrary.Models.Post>();
                int toUserId = SelectedUser.UserId;

                bool noReason = string.IsNullOrWhiteSpace(Input.Reason);

                if (noReason)
                {
                    TempData["AlertTitle"] = "No reason.";
                    TempData["AlertText"] = "No reason was detected in form.";
                    TempData["AlertColor"] = "red";

                    return RedirectToPage();
                }

                var result = await _flagUserService.FlagUser(fromUserId, toUserId, Input.Reason);
                if (result)
                {
                    TempData["AlertTitle"] = "Success.";
                    TempData["AlertText"] = "You have successfully flagged the user.";
                    TempData["AlertColor"] = "green";
                }
                else
                {
                    TempData["AlertTitle"] = "Already Flagged.";
                    TempData["AlertText"] = "User has been already flagged.";
                    TempData["AlertColor"] = "yellow";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["AlertTitle"] = "Error.";
                TempData["AlertText"] = ex.Message;
                TempData["AlertColor"] = "red";
                return RedirectToPage();
            }
        }
    }
}
