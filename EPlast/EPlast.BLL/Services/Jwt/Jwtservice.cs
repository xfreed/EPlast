﻿using EPlast.BLL.DTO.UserProfiles;
using EPlast.BLL.Interfaces.Jwt;
using EPlast.BLL.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EPlast.BLL.Services.Jwt
{
    public class JwtService : IJwtService
    {
        private readonly JwtOptions _jwtOptions;
        private readonly IUserManagerService _userManagerService;

        public JwtService(IOptions<JwtOptions> jwtOptions, IUserManagerService userManagerService)
        {
            _jwtOptions = jwtOptions.Value;
            _userManagerService = userManagerService;
        }

        ///<inheritdoc/>
        public async Task<string> GenerateJWTTokenAsync(UserDTO userDTO)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userDTO.Email),
                new Claim(JwtRegisteredClaimNames.NameId, userDTO.Id),
                new Claim(JwtRegisteredClaimNames.FamilyName, userDTO.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            var roles = await _userManagerService.GetRolesAsync(userDTO);
            claims.AddRange(roles.Select(role => new Claim(ClaimsIdentity.DefaultRoleClaimType, role)));
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
              issuer: _jwtOptions.Issuer,
              audience: _jwtOptions.Audience,
              claims: claims,
              expires: DateTime.Now.AddMinutes(_jwtOptions.Time),
              signingCredentials: creds);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(token);
        }
    }
}
