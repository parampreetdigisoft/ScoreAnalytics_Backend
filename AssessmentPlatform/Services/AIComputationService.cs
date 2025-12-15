using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AssessmentPlatform.Services
{
    public class AIComputationService : IAIComputationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly Download _download;
        public AIComputationService(ApplicationDbContext context, IAppLogger appLogger, Download download)
        {
            _context = context;
            _appLogger = appLogger;
            _download = download;
        }
        public async Task<PaginationResponse<AiCitySummeryDto>> GetAICities(AiCitySummeryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                IQueryable<AICityScore> baseQuery = _context.AICityScores;

                if (userRole == UserRole.Analyst || userRole == UserRole.Evaluator)
                {
                    // Allowed city IDs
                    var allowedCityIds = request.CityID.HasValue
                        ? new List<int> { request.CityID.Value }   // <-- FIXED
                        : await _context.UserCityMappings
                                .Where(x => !x.IsDeleted && x.UserID == userID)
                                .Select(x => x.CityID)
                                .Distinct()
                                .ToListAsync();

                    baseQuery = baseQuery.Where(x => allowedCityIds.Contains(x.CityID));
                }
                else if (userRole == UserRole.CityUser)
                {
                    var allowedCityIds = request.CityID.HasValue
                        ? new List<int> { request.CityID.Value }
                        : await _context.PublicUserCityMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.CityID)
                                .Distinct()
                                .ToListAsync();

                    baseQuery = baseQuery.Where(x => allowedCityIds.Contains(x.CityID));
                }
                else
                {
                    // Admin
                    if (request.CityID.HasValue)
                    {
                        baseQuery = baseQuery.Where(x => x.CityID == request.CityID.Value);
                    }
                }

                var query = baseQuery
                    .Include(x => x.City)
                    .Select(x => new AiCitySummeryDto
                    {
                        CityID = x.CityID,
                        State = x.City.State ?? "",
                        CityName = x.City.CityName ?? "",
                        Country = x.City.Country ?? "",
                        Image = x.City.Image ?? "",
                        ScoringYear = x.Year,
                        AIScore = x.AIScore,
                        AIProgress = x.AIProgress,
                        EvaluatorProgress = x.EvaluatorProgress,
                        Discrepancy = x.Discrepancy,
                        ConfidenceLevel = x.ConfidenceLevel,
                        EvidenceSummary = x.EvidenceSummary,
                        CrossPillarPatterns = x.CrossPillarPatterns,
                        InstitutionalCapacity = x.InstitutionalCapacity,
                        EquityAssessment = x.EquityAssessment,
                        SustainabilityOutlook = x.SustainabilityOutlook,
                        StrategicRecommendations = x.StrategicRecommendations,
                        DataTransparencyNote = x.DataTransparencyNote
                    });

                return await query.ApplyPaginationAsync(request);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetCitiesAsync", ex);
                return new PaginationResponse<AiCitySummeryDto>();
            }
        }
    }
}
