﻿using AutoMapper;
using EPlast.BLL.DTO.AnnualReport;
using EPlast.BLL.Interfaces.City;
using EPlast.BLL.Services.Interfaces;
using EPlast.DataAccess.Entities;
using EPlast.DataAccess.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EPlast.BLL.Services
{
    public class AnnualReportService : IAnnualReportService
    {
        private const string CityAdminTypeName = "Голова Станиці";

        private readonly AdminType _cityAdminType;

        private readonly IRepositoryWrapper _repositoryWrapper;
        private readonly UserManager<User> _userManager;
        private readonly ICityAccessService _cityAccessService;
        private readonly IMapper _mapper;

        public AnnualReportService(IRepositoryWrapper repositoryWrapper, UserManager<User> userManager, ICityAccessService cityAccessService, IMapper mapper)
        {
            _repositoryWrapper = repositoryWrapper;
            _userManager = userManager;
            _cityAccessService = cityAccessService;
            _mapper = mapper;
            _cityAdminType = _repositoryWrapper.AdminType
                .FindByCondition(at => at.AdminTypeName == CityAdminTypeName)
                .FirstOrDefault();
        }

        ///<inheritdoc/>
        public async Task<AnnualReportDTO> GetByIdAsync(ClaimsPrincipal claimsPrincipal, int id)
        {
            var annualReport = await _repositoryWrapper.AnnualReports.GetFirstOrDefaultAsync(
                    predicate: a => a.ID == id,
                    include: source => source
                        .Include(a => a.MembersStatistic)
                        .Include(a => a.CityManagement)
                            .ThenInclude(c => c.CityAdminNew)
                        .Include(a => a.City));
            return await _cityAccessService.HasAccessAsync(claimsPrincipal, annualReport.CityId) ? _mapper.Map<AnnualReport, AnnualReportDTO>(annualReport)
                : throw new UnauthorizedAccessException();
        }

        ///<inheritdoc/>
        public async Task<IEnumerable<AnnualReportDTO>> GetAllAsync(ClaimsPrincipal claimsPrincipal)
        {
            var annualReports = await _repositoryWrapper.AnnualReports.GetAllAsync(
                    include: source => source
                        .Include(ar => ar.User)
                        .Include(ar => ar.City)
                            .ThenInclude(c => c.Region));
            var citiesDTO = await _cityAccessService.GetCitiesAsync(claimsPrincipal);
            var filteredAnnualReports = annualReports.Where(ar => citiesDTO.Any(c => c.ID == ar.CityId));
            return _mapper.Map<IEnumerable<AnnualReport>, IEnumerable<AnnualReportDTO>>(filteredAnnualReports);
        }

        ///<inheritdoc/>
        public async Task CreateAsync(ClaimsPrincipal claimsPrincipal, AnnualReportDTO annualReportDTO)
        {
            var city = await _repositoryWrapper.City.GetFirstOrDefaultAsync(
                predicate: a => a.ID == annualReportDTO.CityId);
            if (!await _cityAccessService.HasAccessAsync(claimsPrincipal, city.ID))
            {
                throw new UnauthorizedAccessException();
            }
            if (await CheckCreated(city.ID))
            {
                throw new InvalidOperationException();
            }
            var annualReport = _mapper.Map<AnnualReportDTO, AnnualReport>(annualReportDTO);
            var user = await _userManager.GetUserAsync(claimsPrincipal);
            annualReport.UserId = user.Id;
            annualReport.Date = DateTime.Now;
            annualReport.Status = AnnualReportStatus.Unconfirmed;
            await _repositoryWrapper.AnnualReports.CreateAsync(annualReport);
            await _repositoryWrapper.SaveAsync();
        }

        ///<inheritdoc/>
        public async Task EditAsync(ClaimsPrincipal claimsPrincipal, AnnualReportDTO annualReportDTO)
        {
            var annualReport = await _repositoryWrapper.AnnualReports.GetFirstOrDefaultAsync(
                    predicate: a => a.ID == annualReportDTO.ID && a.CityId == annualReportDTO.CityId && a.UserId == annualReportDTO.UserId
                        && a.Date.Date == annualReportDTO.Date.Date && a.Status == AnnualReportStatus.Unconfirmed);
            if (annualReportDTO.Status != AnnualReportStatusDTO.Unconfirmed)
            {
                throw new InvalidOperationException();
            }
            if (!await _cityAccessService.HasAccessAsync(claimsPrincipal, annualReport.CityId))
            {
                throw new UnauthorizedAccessException();
            }
            annualReport = _mapper.Map<AnnualReportDTO, AnnualReport>(annualReportDTO);
            _repositoryWrapper.AnnualReports.Update(annualReport);
            await _repositoryWrapper.SaveAsync();
        }

        ///<inheritdoc/>
        public async Task ConfirmAsync(ClaimsPrincipal claimsPrincipal, int id)
        {
            var annualReport = await _repositoryWrapper.AnnualReports.GetFirstOrDefaultAsync(
                    predicate: a => a.ID == id && a.Status == AnnualReportStatus.Unconfirmed,
                    include: source => source
                        .Include(a => a.CityManagement));
            if (!await _cityAccessService.HasAccessAsync(claimsPrincipal, annualReport.CityId))
            {
                throw new UnauthorizedAccessException();
            }
            annualReport.Status = AnnualReportStatus.Confirmed;
            await ChangeCityAdministrationAsync(annualReport);
            await ChangeCityLegalStatusAsync(annualReport);
            _repositoryWrapper.AnnualReports.Update(annualReport);
            await SaveLastConfirmedAsync(annualReport.CityId);
            await _repositoryWrapper.SaveAsync();
        }

        ///<inheritdoc/>
        public async Task CancelAsync(ClaimsPrincipal claimsPrincipal, int id)
        {
            var annualReport = await _repositoryWrapper.AnnualReports.GetFirstOrDefaultAsync(
                    predicate: a => a.ID == id && a.Status == AnnualReportStatus.Confirmed,
                    include: source => source
                        .Include(ar => ar.CityManagement));
            if (!await _cityAccessService.HasAccessAsync(claimsPrincipal, annualReport.CityId))
            {
                throw new UnauthorizedAccessException();
            }
            annualReport.Status = AnnualReportStatus.Unconfirmed;
            var cityAdministrationRevertPoint = annualReport.CityManagement.CityAdminOldId ?? default;
            var cityLegalStatusRevertPoint = annualReport.CityManagement.CityLegalStatusOldId ?? default;
            annualReport.CityManagement.CityAdminOldId = annualReport.CityManagement.CityLegalStatusOldId = null;
            _repositoryWrapper.AnnualReports.Update(annualReport);
            await RevertCityAdministrationAsync(cityAdministrationRevertPoint, annualReport.CityId);
            await RevertCityLegalStatusAsync(cityLegalStatusRevertPoint, annualReport.CityId);
            await _repositoryWrapper.SaveAsync();
        }

        ///<inheritdoc/>
        public async Task DeleteAsync(ClaimsPrincipal claimsPrincipal, int id)
        {
            var annualReport = await _repositoryWrapper.AnnualReports.GetFirstOrDefaultAsync(
                    predicate: a => a.ID == id && a.Status == AnnualReportStatus.Unconfirmed);
            if (!await _cityAccessService.HasAccessAsync(claimsPrincipal, annualReport.CityId))
            {
                throw new UnauthorizedAccessException();
            }
            _repositoryWrapper.AnnualReports.Delete(annualReport);
            await _repositoryWrapper.SaveAsync();
        }

        ///<inheritdoc/>
        public async Task<bool> CheckCreated(ClaimsPrincipal claimsPrincipal, int cityId)
        {
            var city = await _repositoryWrapper.City.GetFirstOrDefaultAsync(
                predicate: a => a.ID == cityId);
            if (!await _cityAccessService.HasAccessAsync(claimsPrincipal, city.ID))
            {
                throw new UnauthorizedAccessException();
            }
            return await CheckCreated(city.ID);
        }

        private async Task SaveLastConfirmedAsync(int cityId)
        {
            var annualReport = await _repositoryWrapper.AnnualReports.GetFirstOrDefaultAsync(
                predicate: a => a.CityId == cityId && a.Status == AnnualReportStatus.Confirmed);
            if (annualReport != null)
            {
                annualReport.Status = AnnualReportStatus.Saved;
                _repositoryWrapper.AnnualReports.Update(annualReport);
            }
        }

        private async Task ChangeCityAdministrationAsync(AnnualReport annualReport)
        {
            var oldCityAdministration = await _repositoryWrapper.CityAdministration.GetFirstOrDefaultAsync(
                predicate: c => c.CityId == annualReport.CityId && (c.EndDate == null || c.EndDate > DateTime.Now) && c.AdminTypeId == _cityAdminType.ID);
            if (oldCityAdministration != null && oldCityAdministration.UserId != annualReport.CityManagement.UserId)
            {
                var user = await _userManager.FindByIdAsync(oldCityAdministration.UserId);
                await _userManager.RemoveFromRoleAsync(user, _cityAdminType.AdminTypeName);
                oldCityAdministration.EndDate = DateTime.Now;
                _repositoryWrapper.CityAdministration.Update(oldCityAdministration);
            }
            if ((oldCityAdministration == null || oldCityAdministration.UserId != annualReport.CityManagement.UserId) && annualReport.CityManagement.UserId != null)
            {
                var user = await _userManager.FindByIdAsync(annualReport.CityManagement.UserId);
                await _userManager.AddToRoleAsync(user, _cityAdminType.AdminTypeName);
                await _repositoryWrapper.CityAdministration.CreateAsync(new CityAdministration
                {
                    CityId = annualReport.CityId,
                    UserId = annualReport.CityManagement.UserId,
                    AdminTypeId = _cityAdminType.ID,
                    StartDate = DateTime.Now
                });
            }
            annualReport.CityManagement.CityAdminOldId = oldCityAdministration?.ID;
        }

        private async Task ChangeCityLegalStatusAsync(AnnualReport annualReport)
        {
            var oldCityLegalStatus = await _repositoryWrapper.CityLegalStatuses.GetFirstOrDefaultAsync(
                predicate: c => c.CityId == annualReport.CityId && (c.DateFinish == null || c.DateFinish > DateTime.Now));
            if (oldCityLegalStatus != null && oldCityLegalStatus.LegalStatusType != annualReport.CityManagement.CityLegalStatusNew)
            {
                oldCityLegalStatus.DateFinish = DateTime.Now;
                _repositoryWrapper.CityLegalStatuses.Update(oldCityLegalStatus);
            }
            if (oldCityLegalStatus == null || oldCityLegalStatus.LegalStatusType != annualReport.CityManagement.CityLegalStatusNew)
            {
                await _repositoryWrapper.CityLegalStatuses.CreateAsync(new CityLegalStatus
                {
                    CityId = annualReport.CityId,
                    LegalStatusType = annualReport.CityManagement.CityLegalStatusNew,
                    DateStart = DateTime.Now
                });
            }
            annualReport.CityManagement.CityLegalStatusOldId = oldCityLegalStatus?.Id;
        }

        private async Task RevertCityAdministrationAsync(int revertPointId, int cityId)
        {
            var cityAdministraions = await _repositoryWrapper.CityAdministration.GetAllAsync(
                predicate: c => c.ID > revertPointId && c.CityId == cityId && c.AdminTypeId == _cityAdminType.ID);
            foreach (var cityAdministration in cityAdministraions)
            {
                var user = await _userManager.FindByIdAsync(cityAdministration.UserId);
                await _userManager.RemoveFromRoleAsync(user, _cityAdminType.AdminTypeName);
                _repositoryWrapper.CityAdministration.Delete(cityAdministration);
            }
            var cityAdministrationRevertPoint = await _repositoryWrapper.CityAdministration.GetFirstOrDefaultAsync(
                predicate: c => c.ID == revertPointId);
            if (cityAdministrationRevertPoint != null)
            {
                var user = await _userManager.FindByIdAsync(cityAdministrationRevertPoint.UserId);
                await _userManager.AddToRoleAsync(user, _cityAdminType.AdminTypeName);
                cityAdministrationRevertPoint.EndDate = null;
                _repositoryWrapper.CityAdministration.Update(cityAdministrationRevertPoint);
            }
        }

        private async Task RevertCityLegalStatusAsync(int revertPointId, int cityId)
        {
            var cityLegalStatuses = await _repositoryWrapper.CityLegalStatuses.GetAllAsync(
                predicate: c => c.Id > revertPointId && c.CityId == cityId);
            foreach (var cityLegalStatus in cityLegalStatuses)
            {
                _repositoryWrapper.CityLegalStatuses.Delete(cityLegalStatus);
            }
            var cityLegalStatusRevertPoint = await _repositoryWrapper.CityLegalStatuses.GetFirstOrDefaultAsync(
                predicate: c => c.Id == revertPointId);
            if (cityLegalStatusRevertPoint != null)
            {
                cityLegalStatusRevertPoint.DateFinish = null;
                _repositoryWrapper.CityLegalStatuses.Update(cityLegalStatusRevertPoint);
            }
        }

        private async Task<bool> CheckCreated(int cityId)
        {
            return await _repositoryWrapper.AnnualReports.GetFirstOrDefaultAsync(
                predicate: a => a.CityId == cityId && (a.Status == AnnualReportStatus.Unconfirmed || a.Date.Year == DateTime.Now.Year)) != null;
        }
    }
}