﻿using AutoMapper;
using EPlast.BussinessLayer.DTO.City;
using EPlast.BussinessLayer.Interfaces.City;
using EPlast.BussinessLayer.Services.Interfaces;
using EPlast.ViewModels.City;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EPlast.Controllers
{
    public class CityController : Controller
    {
        private readonly ILoggerService<CityController> _logger;
        private readonly ICityService _cityService;
        private readonly IMapper _mapper;

        public CityController(ILoggerService<CityController> logger, ICityService cityService, IMapper mapper)
        {
            _cityService = cityService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            return View(_mapper.Map<IEnumerable<CityDTO>, IEnumerable<CityViewModel>>(await _cityService.GetAllDTOAsync()));
        }

        public async Task<IActionResult> CityProfile(int cityId)
        {
            try
            {
                CityProfileDTO cityProfileDto = await _cityService.CityProfileAsync(cityId);
                if (cityProfileDto == null)
                {
                    return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status404NotFound });
                }
                return View(_mapper.Map<CityProfileDTO, CityProfileViewModel>(await _cityService.CityProfileAsync(cityId)));

            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        public async Task<IActionResult> CityMembers(int cityId)
        {
            try
            {
                CityProfileDTO cityProfileDto = await _cityService.CityMembersAsync(cityId);
                if (cityProfileDto == null)
                {
                    return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status404NotFound });
                }
                return View(_mapper.Map<CityProfileDTO, CityProfileViewModel>(await _cityService.CityMembersAsync(cityId)));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        public async Task<IActionResult> CityFollowers(int cityId)
        {
            try
            {
                CityProfileDTO cityProfile = await _cityService.CityFollowersAsync(cityId);
                if (cityProfile == null)
                {
                    return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status404NotFound });
                }
                return View(_mapper.Map<CityProfileDTO, CityProfileViewModel>(cityProfile));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        public async Task<IActionResult> CityAdmins(int cityId)
        {
            try
            {
                CityProfileDTO cityProfileDto = await _cityService.CityAdminsAsync(cityId);
                if (cityProfileDto == null)
                {
                    return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status404NotFound });
                }
                return View(_mapper.Map<CityProfileDTO, CityProfileViewModel>(await _cityService.CityAdminsAsync(cityId)));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int cityId)
        {
            try
            {
                CityProfileDTO cityProfileDto = await _cityService.EditAsync(cityId);
                if (cityProfileDto == null)
                {
                    return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status404NotFound });
                }
                return View(_mapper.Map<CityProfileDTO, CityProfileViewModel>(await _cityService.EditAsync(cityId)));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CityProfileViewModel model, IFormFile file)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View("Edit", model.City.ID);
                }
                await _cityService.EditAsync(_mapper.Map<CityProfileViewModel, CityProfileDTO>(model), file);
                _logger.LogInformation($"City {model.City.Name} was edited profile and saved in the database");
                return RedirectToAction("CityProfile", "City", new { cityid = model.City.ID });

            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }

        }

        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                return View(new CityProfileViewModel());
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(CityProfileViewModel model, IFormFile file)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View("Create", model);
                }
                int cityId = await _cityService.CreateAsync(_mapper.Map<CityProfileViewModel, CityProfileDTO>(model), file);
                return RedirectToAction("CityProfile", "City", new { cityid = cityId });

            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        public async Task<IActionResult> Details(int cityId)
        {
            try
            {
                CityDTO cityDto = await _cityService.GetByIdAsync(cityId);
                if (cityDto == null)
                {
                    return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status404NotFound });
                }
                return View(_mapper.Map<CityDTO, CityViewModel>(cityDto));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

        public async Task<IActionResult> CityDocuments(int cityId)
        {
            try
            {
                CityProfileDTO cityProfileDto = await _cityService.CityDocumentsAsync(cityId);
                if (cityProfileDto == null)
                {
                    return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status404NotFound });
                }
                return View(_mapper.Map<CityProfileDTO, CityProfileViewModel>(cityProfileDto));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception :{e.Message}");
                return RedirectToAction("HandleError", "Error", new { code = StatusCodes.Status505HttpVersionNotsupported });
            }
        }

    }
}