﻿using AutoMapper;
using EPlast.BLL.DTO.Events;
using EPlast.ViewModels.Events;

namespace EPlast.Mapping.Events
{
    public class EventProfile : Profile
    {
        public EventProfile()
        {
            CreateMap<EventDTO, EventViewModel>()
                .ForMember(d => d.Event, s => s.MapFrom(f => f.Event));
        }
    }
}
