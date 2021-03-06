﻿using AutoMapper;
using EPlast.BLL.DTO;
using EPlast.BLL.DTO.Account;
using EPlast.BLL.DTO.UserProfiles;
using EPlast.BLL.Interfaces;
using EPlast.BLL.Interfaces.Logging;
using EPlast.BLL.Interfaces.UserProfiles;
using EPlast.BLL.Services.Interfaces;
using EPlast.Resources;
using EPlast.ViewModels;
using EPlast.ViewModels.UserInformation;
using EPlast.ViewModels.UserInformation.UserProfile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace EPlast.Controllers
{
    [Controller]
    public class AuthController : Controller
    {
        private readonly IAuthService _AuthService;
        private readonly IMapper _mapper;
        private readonly IUserService _userService;
        private readonly INationalityService _nationalityService;
        private readonly IEducationService _educationService;
        private readonly IReligionService _religionService;
        private readonly IWorkService _workService;
        private readonly IGenderService _genderService;
        private readonly IDegreeService _degreeService;
        private readonly IUserManagerService _userManagerService;
        private readonly IConfirmedUsersService _confirmedUserService;
        private readonly ILoggerService<AuthController> _loggerService;
        private readonly IStringLocalizer<AuthenticationErrors> _resourceForErrors;

        public AuthController(IUserService userService,
            INationalityService nationalityService,
            IEducationService educationService,
            IReligionService religionService,
            IWorkService workService,
            IGenderService genderService,
            IDegreeService degreeService,
            IConfirmedUsersService confirmedUserService,
            IUserManagerService userManagerService,
            IMapper mapper,
            ILoggerService<AuthController> loggerService,
            IAuthService AuthService,
            IStringLocalizer<AuthenticationErrors> resourceForErrors)
        {
            _AuthService = AuthService;
            _userService = userService;
            _nationalityService = nationalityService;
            _religionService = religionService;
            _degreeService = degreeService;
            _workService = workService;
            _educationService = educationService;
            _genderService = genderService;
            _confirmedUserService = confirmedUserService;
            _mapper = mapper;
            _userManagerService = userManagerService;
            _loggerService = loggerService;
            _resourceForErrors = resourceForErrors;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl)
        {
            try
            {
                LoginViewModel loginViewModel = new LoginViewModel
                {
                    ReturnUrl = returnUrl,
                    ExternalLogins = (await _AuthService.GetAuthSchemesAsync()).ToList()
                };
                return View(loginViewModel);
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: {e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel loginVM, string returnUrl)
        {
            try
            {
                loginVM.ReturnUrl = returnUrl;
                loginVM.ExternalLogins = (await _AuthService.GetAuthSchemesAsync()).ToList();

                if (ModelState.IsValid)
                {
                    var user = await _AuthService.FindByEmailAsync(loginVM.Email);
                    if (user == null)
                    {
                        ModelState.AddModelError("", _resourceForErrors["Login-NotRegistered"]);
                        return View(loginVM);
                    }
                    else
                    {
                        if (!await _AuthService.IsEmailConfirmedAsync(user))
                        {
                            ModelState.AddModelError("", _resourceForErrors["Login-NotConfirmed"]);
                            return View(loginVM);
                        }
                    }
                    var result = await _AuthService.SignInAsync(_mapper.Map<LoginDto>(loginVM));
                    if (result.IsLockedOut)
                    {
                        return RedirectToAction("AccountLocked", "Account");
                    }
                    if (result.Succeeded)
                    {
                        return RedirectToAction("UserProfile", "Account");
                    }
                    else
                    {
                        ModelState.AddModelError("", _resourceForErrors["Login-InCorrectPassword"]);
                        return View(loginVM);
                    }
                }
                return View("Login", loginVM);
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: {e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View("Register");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel registerVM)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ModelState.AddModelError("", _resourceForErrors["Register-InCorrectData"]);
                    return View("Register");
                }

                var registeredUser = await _AuthService.FindByEmailAsync(registerVM.Email);
                if (registeredUser != null)
                {
                    ModelState.AddModelError("", _resourceForErrors["Register-RegisteredUser"]);
                    return View("Register");
                }
                else
                {
                    var result = await _AuthService.CreateUserAsync(_mapper.Map<RegisterViewModel, RegisterDto>(registerVM));
                    if (!result.Succeeded)
                    {
                        ModelState.AddModelError("", _resourceForErrors["Register-InCorrectPassword"]);
                        return View("Register");
                    }
                    else
                    {
                        string code = await _AuthService.AddRoleAndTokenAsync(_mapper.Map<RegisterViewModel, RegisterDto>(registerVM));
                        var userDto = await _AuthService.FindByEmailAsync(registerVM.Email);
                        string confirmationLink = Url.Action(
                            nameof(ConfirmingEmail),
                            "Account",
                            new { code = code, userId = userDto.Id },
                              protocol: HttpContext.Request.Scheme);
                        await _AuthService.SendEmailRegistr(confirmationLink, userDto);
                        return View("AcceptingEmail");
                    }
                }
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: {e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [HttpGet]
        public IActionResult ConfirmedEmail()
        {
            return View("ConfirmedEmail");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResendEmailForRegistering(string userId)
        {
            var userDto = await _AuthService.FindByIdAsync(userId);
            if (userDto == null)
            {
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
            string code = await _AuthService.GenerateConfToken(userDto);
            var confirmationLink = Url.Action(
                nameof(ConfirmingEmail),
                "Account",
                new { code = code, userId = userDto.Id },
                protocol: HttpContext.Request.Scheme);

            await _AuthService.SendEmailRegistr(confirmationLink, userDto);
            return View("ResendEmailConfirmation");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmingEmail(string userId, string code)
        {
            var userDto = await _AuthService.FindByIdAsync(userId);
            if (userDto == null)
            {
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
            int totalTime = _AuthService.GetTimeAfterRegistr(userDto);
            if (totalTime < 180)
            {
                if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(code))
                {
                    return RedirectToAction("HandleError", "Error", new { code = 500 });
                }

                var result = await _AuthService.ConfirmEmailAsync(userDto.Id, code);

                if (result.Succeeded)
                {
                    return RedirectToAction("ConfirmedEmail", "Account");
                }
                else
                {
                    return RedirectToAction("HandleError", "Error", new { code = 500 });
                }
            }
            else
            {
                return View("ConfirmEmailNotAllowed", userDto);
            }
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccountLocked()
        {
            return View("AccountLocked");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Logout()
        {
            _AuthService.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View("ForgotPassword");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel forgotpasswordVM)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var userDto = await _AuthService.FindByEmailAsync(forgotpasswordVM.Email);
                    if (userDto == null || !(await _AuthService.IsEmailConfirmedAsync(userDto)))
                    {
                        ModelState.AddModelError("", _resourceForErrors["Forgot-NotRegisteredUser"]);
                        return View("ForgotPassword");
                    }
                    string code = await _AuthService.GenerateResetTokenAsync(userDto);
                    string confirmationLink = Url.Action(
                        nameof(ResetPassword),
                        "Account",
                        new { userId = userDto.Id, code = HttpUtility.UrlEncode(code) },
                        protocol: HttpContext.Request.Scheme);
                    await _AuthService.SendEmailReseting(confirmationLink, _mapper.Map<ForgotPasswordDto>(forgotpasswordVM));
                    return View("ForgotPasswordConfirmation");
                }
                return View("ForgotPassword");
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: {e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string userId, string code = null)
        {
            var userDto = await _AuthService.FindByIdAsync(userId);
            if (userDto == null)
            {
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
            int totalTime = _AuthService.GetTimeAfterReset(userDto);
            if (totalTime < 180)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return RedirectToAction("HandleError", "Error", new { code = 500 });
                }
                else
                {
                    return View("ResetPassword");
                }
            }
            else
            {
                return View("ResetPasswordNotAllowed", userDto);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel resetpasswordVM)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View("ResetPassword");
                }
                var userDto = await _AuthService.FindByEmailAsync(resetpasswordVM.Email);
                if (userDto == null)
                {
                    ModelState.AddModelError("", _resourceForErrors["Reset-NotRegisteredUser"]);
                    return View("ResetPassword");
                }
                var result = await _AuthService.ResetPasswordAsync(userDto.Id, _mapper.Map<ResetPasswordDto>(resetpasswordVM));
                if (result.Succeeded)
                {
                    await _AuthService.CheckingForLocking(userDto);
                    return View("ResetPasswordConfirmation");
                }
                else
                {
                    ModelState.AddModelError("", _resourceForErrors["Reset-PasswordProblems"]);
                    return View("ResetPassword");
                }
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: {e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ChangePassword()
        {
            var userDto = await _AuthService.GetUserAsync(User);
            var result = userDto.SocialNetworking;
            if (result != true)
            {
                return View("ChangePassword");
            }
            else
            {
                return View("ChangePasswordNotAllowed");
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel changepasswordModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var userDto = await _AuthService.GetUserAsync(User);
                    if (userDto == null)
                    {
                        return RedirectToAction("Login");
                    }
                    var result = await _AuthService.ChangePasswordAsync(userDto.Id, _mapper.Map<ChangePasswordDto>(changepasswordModel));
                    if (!result.Succeeded)
                    {
                        ModelState.AddModelError("", _resourceForErrors["Change-PasswordProblems"]);
                        return View("ChangePassword");
                    }
                    _AuthService.RefreshSignInAsync(userDto);
                    return View("ChangePasswordConfirmation");
                }
                else
                {
                    return View("ChangePassword");
                }
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: {e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult ExternalLogin(string provider, string returnUrl)
        {
            string redirectUrl = Url.Action("ExternalLoginCallBack",
                "Account",
                new { ReturnUrl = returnUrl });
            AuthenticationProperties properties = _AuthService.GetAuthProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallBack(string returnUrl = null, string remoteError = null)
        {
            try
            {
                returnUrl = returnUrl ?? Url.Content("~/Account/UserProfile");
                LoginViewModel loginViewModel = new LoginViewModel
                {
                    ReturnUrl = returnUrl,
                    ExternalLogins = (await _AuthService.GetAuthSchemesAsync()).ToList()
                };

                if (remoteError != null)
                {
                    ModelState.AddModelError("", _resourceForErrors["Error-ExternalLoginProvider"]);
                    return View("Login", loginViewModel);
                }
                var info = await _AuthService.GetInfoAsync();
                if (info == null)
                {
                    ModelState.AddModelError(string.Empty, _resourceForErrors["Error-ExternalLoginInfo"]);
                    return View("Login", loginViewModel);
                }

                var signInResult = await _AuthService.GetSignInResultAsync(info);
                if (signInResult.Succeeded)
                {
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                    if (info.LoginProvider.ToString() == "Google")
                    {
                        if (email != null)
                        {
                            await _AuthService.GoogleAuthentication(email, info);
                            return LocalRedirect(returnUrl);
                        }
                    }
                    else if (info.LoginProvider.ToString() == "Facebook")
                    {
                        await _AuthService.FacebookAuthentication(email, info);
                        return LocalRedirect(returnUrl);
                    }
                    return View("Error");
                }
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: {e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> UserProfile(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    // _logger.Log(LogLevel.Error, "User id is null");
                    // return RedirectToAction("HandleError", "Error", new { code = 500 });
                    userId = await _userManagerService.GetUserIdAsync(User);
                }

                var user = await _userService.GetUserAsync(userId);
                var time = await _userService.CheckOrAddPlastunRoleAsync(_mapper.Map<UserDTO, UserViewModel>(user).Id, user.RegistredOn);
                var isUserPlastun = await _userManagerService.IsInRoleAsync(user, "Пластун");

                var model = new PersonalDataViewModel
                {
                    User = _mapper.Map<UserDTO, UserViewModel>(user),
                    TimeToJoinPlast = time,
                    IsUserPlastun = isUserPlastun
                };

                return View(model);
            }
            catch
            {
                _loggerService.LogError("Smth went wrong");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Approvers(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    _loggerService.LogError("User id is null");
                    return RedirectToAction("HandleError", "Error", new { code = 500 });
                }

                var user = await _userService.GetUserAsync(userId);
                var confirmedUsers = _userService.GetConfirmedUsers(user);
                var canApprove = await _userService.CanApproveAsync(confirmedUsers, userId, User);
                var time = await _userService.CheckOrAddPlastunRoleAsync(user.Id, user.RegistredOn);
                var clubApprover = _userService.GetClubAdminConfirmedUser(user);
                var cityApprover = _userService.GetCityAdminConfirmedUser(user);

                if (user != null)
                {
                    var model = new UserApproversViewModel
                    {
                        User = _mapper.Map<UserDTO, UserViewModel>(user),
                        canApprove = canApprove,
                        TimeToJoinPlast = time,
                        ConfirmedUsers = _mapper.Map<IEnumerable<ConfirmedUserDTO>, IEnumerable<ConfirmedUserViewModel>>(confirmedUsers),
                        ClubApprover = _mapper.Map<ConfirmedUserDTO, ConfirmedUserViewModel>(clubApprover),
                        CityApprover = _mapper.Map<ConfirmedUserDTO, ConfirmedUserViewModel>(cityApprover),
                        IsUserHeadOfCity = await _userManagerService.IsInRoleAsync(user, "Голова Станиці"),
                        IsUserHeadOfClub = await _userManagerService.IsInRoleAsync(user, "Голова Куреня"),
                        IsUserHeadOfRegion = await _userManagerService.IsInRoleAsync(user, "Голова Округу"),
                        IsUserPlastun = await _userManagerService.IsInRoleAsync(user, "Пластун"),
                        CurrentUserId = await _userManagerService.GetUserIdAsync(User)
                    };

                    return View(model);
                }
                _loggerService.LogError($"Can`t find this user:{userId}, or smth else");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
            catch
            {
                _loggerService.LogError("Smth went wrong");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        public async Task<IActionResult> ApproveUser(string userId, bool isClubAdmin = false, bool isCityAdmin = false)
        {
            if (userId != null)
            {
                await _confirmedUserService.CreateAsync(User, userId, isClubAdmin, isCityAdmin);
                return RedirectToAction("Approvers", "Account", new { userId = userId });
            }
            _loggerService.LogError("User id is null");
            return RedirectToAction("HandleError", "Error", new { code = 500 });
        }

        [Authorize]
        public async Task<IActionResult> ApproverDelete(int confirmedId, string userId)
        {
            await _confirmedUserService.DeleteAsync(confirmedId);
            _loggerService.LogInformation("Approve succesfuly deleted");
            return RedirectToAction("Approvers", "Account", new { userId = userId });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(string userId)
        {
            if (userId == null)
            {
                _loggerService.LogError("User id is null");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }

            try
            {
                var user = await _userService.GetUserAsync(userId);

                var genders = (from item in await _genderService.GetAllAsync() select new SelectListItem { Text = item.Name, Value = item.ID.ToString() });

                var placeOfStudyUnique = _mapper.Map<IEnumerable<EducationDTO>, IEnumerable<EducationViewModel>>(await _educationService.GetAllGroupByPlaceAsync());
                var specialityUnique = _mapper.Map<IEnumerable<EducationDTO>, IEnumerable<EducationViewModel>>(await _educationService.GetAllGroupBySpecialityAsync());
                var placeOfWorkUnique = _mapper.Map<IEnumerable<WorkDTO>, IEnumerable<WorkViewModel>>(await _workService.GetAllGroupByPlaceAsync());
                var positionUnique = _mapper.Map<IEnumerable<WorkDTO>, IEnumerable<WorkViewModel>>(await _workService.GetAllGroupByPositionAsync());

                var educView = new EducationUserViewModel { PlaceOfStudyID = user.UserProfile.EducationId, SpecialityID = user.UserProfile.EducationId, PlaceOfStudyList = placeOfStudyUnique, SpecialityList = specialityUnique };
                var workView = new WorkUserViewModel { PlaceOfWorkID = user.UserProfile.WorkId, PositionID = user.UserProfile.WorkId, PlaceOfWorkList = placeOfWorkUnique, PositionList = positionUnique };
                var model = new EditUserViewModel()
                {
                    User = _mapper.Map<UserDTO, UserViewModel>(user),
                    Nationalities = _mapper.Map<IEnumerable<NationalityDTO>, IEnumerable<NationalityViewModel>>(await _nationalityService.GetAllAsync()),
                    Religions = _mapper.Map<IEnumerable<ReligionDTO>, IEnumerable<ReligionViewModel>>(await _religionService.GetAllAsync()),
                    EducationView = educView,
                    WorkView = workView,
                    Degrees = _mapper.Map<IEnumerable<DegreeDTO>, IEnumerable<DegreeViewModel>>(await _degreeService.GetAllAsync()),
                    Genders = genders
                };

                return View(model);
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: { e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Edit(EditUserViewModel model, IFormFile file)
        {
            try
            {
                await _userService.UpdateAsync(_mapper.Map<UserViewModel, UserDTO>(model.User), file, model.EducationView.PlaceOfStudyID, model.EducationView.SpecialityID, model.WorkView.PlaceOfWorkID, model.WorkView.PositionID);
                _loggerService.LogInformation($"User {model.User.Email} was edited profile and saved in the database");
                return RedirectToAction("UserProfile");
            }
            catch (Exception e)
            {
                _loggerService.LogError($"Exception: { e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = 500 });
            }
        }
    }
}