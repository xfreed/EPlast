﻿using EPlast.BLL.DTO.City;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccessCity = EPlast.DataAccess.Entities;

namespace EPlast.BLL.Interfaces.City
{
    public interface ICityService
    {
        Task<IEnumerable<DataAccessCity.City>> GetAllAsync();
        Task<IEnumerable<CityDTO>> GetAllDTOAsync();
        Task<CityDTO> GetByIdAsync(int cityId);
        Task<CityProfileDTO> GetCityProfileAsync(int cityId);
        Task<CityProfileDTO> GetCityMembersAsync(int cityId);
        Task<CityProfileDTO> GetCityFollowersAsync(int cityId);
        Task<CityProfileDTO> GetCityAdminsAsync(int cityId);
        Task<CityProfileDTO> GetCityDocumentsAsync(int cityId);
        Task<CityProfileDTO> EditAsync(int cityId);
        Task EditAsync(CityProfileDTO model, IFormFile file);
        Task EditAsync(CityProfileDTO model);
        Task<int> CreateAsync(CityProfileDTO model, IFormFile file);
        Task<int> CreateAsync(CityProfileDTO model);
        Task<string> GetLogoBase64(string logoName);
    }
}