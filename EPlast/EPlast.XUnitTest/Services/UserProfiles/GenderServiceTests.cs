﻿using AutoMapper;
using EPlast.BLL.DTO.UserProfiles;
using EPlast.BLL.Services.UserProfiles;
using EPlast.DataAccess.Entities;
using EPlast.DataAccess.Repositories;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EPlast.XUnitTest.Services.UserProfiles
{
    public class GenderServiceTests
    {
        private Mock<IRepositoryWrapper> _repoWrapper;
        private Mock<IMapper> _mapper;

        public GenderServiceTests()
        {
            _repoWrapper = new Mock<IRepositoryWrapper>();
            _mapper = new Mock<IMapper>();
        }
        [Fact]
        public async Task GetAllTest()
        {
            _repoWrapper.Setup(r => r.Gender.GetAllAsync(null, null)).ReturnsAsync(new List<Gender>().AsQueryable());

            var service = new GenderService(_repoWrapper.Object, _mapper.Object);
            _mapper.Setup(x => x.Map<Gender, GenderDTO>(It.IsAny<Gender>())).Returns(new GenderDTO());
            // Act
            var result = await service.GetAllAsync();
            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<IEnumerable<GenderDTO>>(result);
        }
    }
}
