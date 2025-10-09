﻿using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.PublicDto;
using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    [AllowAnonymous]
    public class PublicService : IPublicService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public PublicService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }
        public async Task<ResultResponseDto<List<PartnerCityResponseDto>>> GetAllCities()
        {
            try
            {
                var result = await _context.Cities.Where(c => c.IsActive && !c.IsDeleted).
                 Select(c => new PartnerCityResponseDto
                 {
                     CityID = c.CityID,
                     State = c.State,
                     CityName = c.CityName,
                     PostalCode = c.PostalCode,
                     Region = c.Region,
                     Country = c.Country
                 }).OrderBy(x => x.CityName).ToListAsync();

                return ResultResponseDto<List<PartnerCityResponseDto>>.Success(result, new string[] { "get All Cities successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCities", ex);
                return ResultResponseDto<List<PartnerCityResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<PartnerCityFilterResponse>> GetPartnerCitiesFilterRecord()
        {
            try
            {
                // Fetch all active cities once
                var activeCities = await _context.Cities
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var res = new PartnerCityFilterResponse
                {
                    Countries = activeCities
                        .Select(x => x.Country)
                        .Distinct()
                        .ToList(),

                    Cities = activeCities
                        .Select(x => new PartnerCityDto
                        {
                            CityID = x.CityID,
                            CityName = x.CityName
                        })
                        .ToList(),

                    Regions = activeCities
                        .Select(x => x.Region)
                        .Where(r => !string.IsNullOrEmpty(r))
                        .Distinct()
                        .ToList()
                };

                return ResultResponseDto<PartnerCityFilterResponse>.Success(
                    res,
                    new List<string> { "Get Cities history successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetPartnerCitiesFilterRecord", ex);
                return ResultResponseDto<PartnerCityFilterResponse>.Failure(
                    new string[] { "Failed to get Partner City filter data" }
                );
            }
        }

        public async Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync()
        {
            try
            {
                var res =  await _context.Pillars
                .OrderBy(p => p.DisplayOrder)
                .Select(x => new PillarResponseDto
                {
                    DisplayOrder = x.DisplayOrder,
                    PillarID = x.PillarID,
                    PillarName = x.PillarName
                }).ToListAsync();
                return ResultResponseDto<List<PillarResponseDto>>.Success(res, new List<string> { "Get Cities history successfully" });

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllPillarAsync", ex);
                return ResultResponseDto<List<PillarResponseDto>>.Failure(new string[] { "Failed to get Piilar detail" });
            }
        }
        public async Task<PaginationResponse<PartnerCityResponseDto>> GetPartnerCities(PartnerCityRequestDto request)
        {
            try
            {
                var year = DateTime.Now.Year;


                var cityQuery =
                   from c in _context.Cities.Where(x => !request.CityID.HasValue || x.CityID == request.CityID)
                   join uc in _context.UserCityMappings on c.CityID equals uc.CityID into ucg
                   from uc in ucg.DefaultIfEmpty()
                   join a in _context.Assessments on uc.UserCityMappingID equals a.UserCityMappingID into ag
                   from a in ag.DefaultIfEmpty()
                   join pa in _context.PillarAssessments.Where(x=> !request.PillarID.HasValue || x.PillarID == request.PillarID) 
                   on a.AssessmentID equals pa.AssessmentID into pag
                   from pa in pag.DefaultIfEmpty()
                   join r in _context.AssessmentResponses on pa.PillarAssessmentID equals r.PillarAssessmentID into rg
                   from r in rg.DefaultIfEmpty()
                   where !c.IsDeleted && 
                    (uc == null || !uc.IsDeleted) &&
                    (a == null || a.UpdatedAt.Year == year) 
                   group r by new
                   {
                       c.CityID,
                       c.Country,
                       c.PostalCode,
                       c.Image,
                       c.State,
                       c.CityName,
                       c.Region,
                       EvaluatorCount = _context.UserCityMappings
                                           .Count(x => x.CityID == c.CityID && !x.IsDeleted)
                   }
                   into g
                   select new PartnerCityResponseDto
                   {
                       CityID = g.Key.CityID,
                       State = g.Key.State,
                       CityName = g.Key.CityName,
                       PostalCode = g.Key.PostalCode,
                       Region = g.Key.Region,
                       Country = g.Key.Country,
                       Image = g.Key.Image,
                       Score = (decimal)g.Sum(x => (int?)x.Score ?? 0) / (g.Key.EvaluatorCount == 0 ? 1 : g.Key.EvaluatorCount),
                       HighScore = g.Max(x=>(int?)x.Score ?? 0),
                       LowerScore = g.Min(x => (int?)x.Score ?? 0),
                       Progress = ((decimal)g.Sum(x => (int?)x.Score ?? 0) * 100) / ((g.Key.EvaluatorCount == 0 ? 1 : g.Key.EvaluatorCount) * 4 * g.Count()),
                   };

                if (!string.IsNullOrWhiteSpace(request.Country))
                {
                    cityQuery = cityQuery.Where(c => c.Country.Contains(request.Country));
                }

                // Only filter by Region if a value is provided
                if (!string.IsNullOrWhiteSpace(request.Region))
                {
                    cityQuery = cityQuery.Where(c => c.Region != null && c.Region.Contains(request.Region));
                }

                var response = await cityQuery.ApplyPaginationAsync(request);

                return response;

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCitiesProgressByUserId", ex);
                return new();
            }
        }
    }
}
