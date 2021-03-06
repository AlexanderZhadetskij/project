﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using crowdfunding.Data;
using crowdfunding.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace crowdfunding.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private UserManager<User> _userManager;
        private IUserValidator<User> _userValidator;
        private IPasswordValidator<User> _passwordValidator;
        private IPasswordHasher<User> _passwordHasher;
        private IWebHostEnvironment _hostingEnvironment;

        public AdminController(
        UserManager<User> userManager,
        IUserValidator<User> userValidator,
        IPasswordValidator<User> passwordValidator,
        IPasswordHasher<User> passwordHasher,
        IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _userValidator = userValidator;
            _passwordValidator = passwordValidator;
            _passwordHasher = passwordHasher;
            _hostingEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Index() => View(_userManager.Users.ToList());

        [HttpPost]
        public async Task<IActionResult> DeleteUsers(string[] selected)
        {
            var users = _userManager.Users.Where(user => selected.Contains(user.Id)).ToList();
            if (users.Count > 0)
            {
                foreach (var item in users)
                {
                    IdentityResult result = await _userManager.DeleteAsync(item);
                    if (!result.Succeeded)
                    {
                        AddErrorsFromResult(result);
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "User Not Found");
            }
            return View("Index", _userManager.Users.ToList());
        }

        private void AddErrorsFromResult(IdentityResult result)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }

        public async Task<IActionResult> Edit(string id)
        {
            User user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                UserModel model = new UserModel { Id = user.Id, Email = user.Email, UserName = user.UserName };
                return View(model);
            }
            else
            {
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(string id, string email, string userName, IFormFile photo, string oldPassword, string newPassword)
        {
            User user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.Email = email;
                user.UserName = userName;

                if (photo != null)
                {
                    var uploads = Path.Combine(_hostingEnvironment.WebRootPath, "uploads");
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + photo.FileName.Split("\\").Last();
                    user.PhotoPath = uniqueFileName;
                    var filePath = Path.Combine(uploads, uniqueFileName);
                    await photo.CopyToAsync(new FileStream(filePath, FileMode.Create));
                }

                IdentityResult validEmail = await _userValidator.ValidateAsync(_userManager, user);
                if (!validEmail.Succeeded)
                {
                    AddErrorsFromResult(validEmail);
                }
                IdentityResult validPass = null;
                if (!string.IsNullOrEmpty(oldPassword))
                {
                    if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword) == PasswordVerificationResult.Failed)
                    {
                        ModelState.AddModelError("", "Old password is incorrect");
                    }

                    validPass = await _passwordValidator.ValidateAsync(_userManager, user, oldPassword);

                    if (validPass.Succeeded)
                    {
                        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
                    }
                    else
                    {
                        AddErrorsFromResult(validPass);
                    }
                }
                if ((validEmail.Succeeded && validPass == null) || (validEmail.Succeeded && oldPassword != string.Empty && newPassword != string.Empty && validPass.Succeeded))
                {
                    IdentityResult result = await _userManager.UpdateAsync(user);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        AddErrorsFromResult(result);
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "User Not Found");
            }
            return View(user);
        }
    }
}