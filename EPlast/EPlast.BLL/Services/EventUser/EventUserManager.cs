﻿using AutoMapper;
using EPlast.BLL.DTO.EventUser;
using EPlast.BLL.DTO.UserProfiles;
using EPlast.BLL.Interfaces.Events;
using EPlast.BLL.Interfaces.EventUser;
using EPlast.DataAccess.Entities;
using EPlast.DataAccess.Entities.Event;
using EPlast.DataAccess.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EPlast.BLL.Services.EventUser
{
    /// <inheritdoc/>
    public class EventUserManager : IEventUserManager
    {
        private readonly IRepositoryWrapper _repoWrapper;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;
        private readonly IParticipantStatusManager _participantStatusManager;
        private readonly IParticipantManager _participantManager;
        private readonly IEventCategoryManager _eventCategoryManager;
        private readonly IEventStatusManager _eventStatusManager;
        private readonly IEventAdministrationTypeManager _eventAdministrationTypeManager;
        private readonly IEventAdmininistrationManager _eventAdmininistrationManager;


        public EventUserManager(IRepositoryWrapper repoWrapper, UserManager<User> userManager,
            IParticipantStatusManager participantStatusManager, IMapper mapper, IParticipantManager participantManager,
            IEventCategoryManager eventCategoryManager, IEventStatusManager eventStatusManager,
            IEventAdministrationTypeManager eventAdministrationTypeManager,
            IEventAdmininistrationManager eventAdmininistrationManager)
        {
            _repoWrapper = repoWrapper;
            _userManager = userManager;
            _participantStatusManager = participantStatusManager;
            _mapper = mapper;
            _participantManager = participantManager;
            _eventCategoryManager = eventCategoryManager;
            _eventStatusManager = eventStatusManager;
            _eventAdministrationTypeManager = eventAdministrationTypeManager;
            _eventAdmininistrationManager = eventAdmininistrationManager;
        }
        public async Task<EventUserDTO> GetCreatedArchivedEvents(string userId, ClaimsPrincipal user)
        {
            var currentUserId = _userManager.GetUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                userId = currentUserId;
            }

            var targetUser = await _repoWrapper.User.GetFirstAsync(predicate: q => q.Id == userId);
            var model = new EventUserDTO
            {
                User = _mapper.Map<User, UserDTO>(targetUser)
            };

            var eventAdmins = await _eventAdmininistrationManager.GetEventAdmininistrationByUserIdAsync(userId);

            model.CreatedEvents = new List<EventGeneralInfoDTO>();
            foreach (var eventAdmin in eventAdmins)
            {
                var eventToAdd = _mapper.Map<Event, EventGeneralInfoDTO>(eventAdmin.Event);
                if (!(eventToAdd.EventDateEnd > DateTime.Now))
                {
                    eventToAdd.EventStatusID = 1;
                    model.CreatedEvents.Add(eventToAdd);
                }
            }

            return model;
        }

        public async Task<EventUserDTO> EventUserAsync(string userId, ClaimsPrincipal user)
        {
            var currentUserId = _userManager.GetUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                userId = currentUserId;
            }

            var targetUser = await _repoWrapper.User.GetFirstAsync(predicate: q => q.Id == userId);
            User userWithRoles = await _userManager.FindByIdAsync(userId);
            var userRoles = await _userManager.GetRolesAsync(userWithRoles);
            var model = new EventUserDTO
            {
                User = _mapper.Map<User, UserDTO>(targetUser),
                UserRoles = userRoles
            };
            var eventAdmins = await _eventAdmininistrationManager.GetEventAdmininistrationByUserIdAsync(userId);
            var participants = await _participantManager.GetParticipantsByUserIdAsync(userId);
            model.CreatedEvents = new List<EventGeneralInfoDTO>();
            foreach (var eventAdmin in eventAdmins)
            {
                var eventToAdd = _mapper.Map<Event, EventGeneralInfoDTO>(eventAdmin.Event);
                if (eventToAdd.EventDateEnd > DateTime.Now)
                {
                    model.CreatedEvents.Add(eventToAdd);
                }
            }

            model.PlanedEvents = new List<EventGeneralInfoDTO>();
            model.VisitedEvents = new List<EventGeneralInfoDTO>();
            foreach (var participant in participants)
            {
                var eventToAdd = _mapper.Map<Event, EventGeneralInfoDTO>(participant.Event);
                if (participant.Event.EventDateEnd >= DateTime.Now)
                {
                    model.PlanedEvents.Add(eventToAdd);
                }
                else if (participant.Event.EventDateEnd < DateTime.Now &&
                         participant.ParticipantStatusId == await _participantStatusManager.GetStatusIdAsync("Учасник"))
                {
                    model.VisitedEvents.Add(eventToAdd);
                }
            }

            return model;
        }

        public async Task<EventCreateDTO> InitializeEventCreateDTOAsync()
        {
            var eventCategories = await _eventCategoryManager.GetDTOAsync();
            var users = _mapper.Map<List<User>, IEnumerable<UserInfoDTO>>((await _repoWrapper.User.GetAllAsync()).ToList());
            var eventTypes = _mapper.Map<List<EventType>, IEnumerable<EventTypeDTO>>((await _repoWrapper.EventType.GetAllAsync()).ToList());

            var model = new EventCreateDTO()
            {
                Users = users,
                EventTypes = eventTypes,
                EventCategories = eventCategories
            };

            return model;
        }

        public async Task<int> CreateEventAsync(EventCreateDTO model)
        {
            model.Event.EventStatusID = await _eventStatusManager.GetStatusIdAsync("Не затверджені");

            var eventToCreate = _mapper.Map<EventCreationDTO, Event>(model.Event);
            var commandantTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Комендант");
            var alternateTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Заступник коменданта");
            var bunchuzhnyiTypeID = await _eventAdministrationTypeManager.GetTypeIdAsync("Бунчужний");
            var pysarTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Писар");

            var administrationList = new List<EventAdministration>
            {
                new EventAdministration
                {
                    UserID = model.Сommandant.UserId,
                    EventAdministrationTypeID = commandantTypeId,
                    EventID = eventToCreate.ID,
                },
                 new EventAdministration
                 {
                    UserID = model.Alternate.UserId,
                    EventAdministrationTypeID = alternateTypeId,
                    EventID = eventToCreate.ID,
                 },
                  new EventAdministration
                  {
                    UserID = model.Bunchuzhnyi.UserId,
                    EventAdministrationTypeID = bunchuzhnyiTypeID,
                    EventID = eventToCreate.ID,
                  },
                   new EventAdministration
                   {
                    UserID = model.Pysar.UserId,
                    EventAdministrationTypeID = pysarTypeId,
                    EventID = eventToCreate.ID,
                   },
            };

            eventToCreate.EventAdministrations = administrationList;

            await _repoWrapper.Event.CreateAsync(eventToCreate);
            await _repoWrapper.SaveAsync();

            return eventToCreate.ID;
        }

        public async Task<EventCreateDTO> InitializeEventEditDTOAsync(int eventId)
        {
            var editedEvent = await _repoWrapper.Event.GetFirstAsync(predicate: i => i.ID == eventId, include: source => source.Include(i => i.EventAdministrations));

            var users = _mapper.Map<List<User>, IEnumerable<UserInfoDTO>>((await _repoWrapper.User.GetAllAsync()).ToList());
            var eventTypes = _mapper.Map<List<EventType>, IEnumerable<EventTypeDTO>>((await _repoWrapper.EventType.GetAllAsync()).ToList());
            var eventCategories = await _eventCategoryManager.GetDTOAsync();
            var commandantTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Комендант");
            var alternateTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Заступник коменданта");
            var bunchuzhnyiTypeID = await _eventAdministrationTypeManager.GetTypeIdAsync("Бунчужний");
            var pysarTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Писар");

            var commandant = _mapper.Map<EventAdministration, EventAdministrationDTO>(await _repoWrapper.EventAdministration.
                GetFirstAsync(predicate: i => i.EventAdministrationTypeID == commandantTypeId && i.EventID == eventId, include: source => source.Include(q => q.User)));
            var alternate = _mapper.Map<EventAdministration, EventAdministrationDTO>(await _repoWrapper.EventAdministration.
               GetFirstAsync(predicate: i => i.EventAdministrationTypeID == alternateTypeId && i.EventID == eventId, include: source => source.Include(q => q.User)));
            var bunchuzhnyi = _mapper.Map<EventAdministration, EventAdministrationDTO>(await _repoWrapper.EventAdministration.
               GetFirstAsync(predicate: i => i.EventAdministrationTypeID == bunchuzhnyiTypeID && i.EventID == eventId, include: source => source.Include(q => q.User)));
            var pysar = _mapper.Map<EventAdministration, EventAdministrationDTO>(await _repoWrapper.EventAdministration.
               GetFirstAsync(predicate: i => i.EventAdministrationTypeID == pysarTypeId && i.EventID == eventId, include: source => source.Include(q => q.User)));
            var model = new EventCreateDTO()
            {
                Event = _mapper.Map<Event, EventCreationDTO>(editedEvent),
                Users = users,
                EventTypes = eventTypes,
                EventCategories = eventCategories,
                Сommandant = commandant,
                Alternate = alternate,
                Bunchuzhnyi = bunchuzhnyi,
                Pysar = pysar
            };

            return model;
        }

        public async Task EditEventAsync(EventCreateDTO model)
        {
            model.Event.EventStatusID = await _eventStatusManager.GetStatusIdAsync("Не затверджені");

            var eventToEdit = _mapper.Map<EventCreationDTO, Event>(model.Event);
            var commandantTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Комендант");
            var alternateTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Заступник коменданта");
            var bunchuzhnyiTypeID = await _eventAdministrationTypeManager.GetTypeIdAsync("Бунчужний");
            var pysarTypeId = await _eventAdministrationTypeManager.GetTypeIdAsync("Писар");

            var commandant = await _repoWrapper.EventAdministration.
                GetFirstAsync(predicate: i => i.EventAdministrationTypeID == commandantTypeId && i.EventID == eventToEdit.ID, include: source => source.Include(q => q.User));
            var alternate = await _repoWrapper.EventAdministration.
               GetFirstAsync(predicate: i => i.EventAdministrationTypeID == alternateTypeId && i.EventID == eventToEdit.ID, include: source => source.Include(q => q.User));
            var bunchuzhnyi = await _repoWrapper.EventAdministration.
               GetFirstAsync(predicate: i => i.EventAdministrationTypeID == bunchuzhnyiTypeID && i.EventID == eventToEdit.ID, include: source => source.Include(q => q.User));
            var pysar = await _repoWrapper.EventAdministration.
               GetFirstAsync(predicate: i => i.EventAdministrationTypeID == pysarTypeId && i.EventID == eventToEdit.ID, include: source => source.Include(q => q.User));
            var administrationList = new List<EventAdministration>
            {
                new EventAdministration
                {
                    UserID = model.Сommandant.UserId,
                    EventAdministrationTypeID = commandantTypeId,
                    EventID = eventToEdit.ID,
                    ID = commandant.ID,
                },
                 new EventAdministration
                 {
                    UserID = model.Alternate.UserId,
                    EventAdministrationTypeID = alternateTypeId,
                    EventID = eventToEdit.ID,
                    ID = alternate.ID,
                 },
                  new EventAdministration
                  {
                    UserID = model.Bunchuzhnyi.UserId,
                    EventAdministrationTypeID = bunchuzhnyiTypeID,
                    EventID = eventToEdit.ID,
                    ID =  bunchuzhnyi.ID,
                  },
                   new EventAdministration
                   {
                    UserID = model.Pysar.UserId,
                    EventAdministrationTypeID = pysarTypeId,
                    EventID = eventToEdit.ID,
                    ID = pysar.ID,
                   },
            };

            eventToEdit.EventAdministrations = administrationList;
            _repoWrapper.Event.Update(eventToEdit);
            await _repoWrapper.SaveAsync();
        }
    }
}