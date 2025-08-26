using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    public class AssessmentResponseService : IAssessmentResponseService
    {
        private readonly ApplicationDbContext _context;
        public AssessmentResponseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AssessmentResponse>> GetAllAsync()
        {
            return await _context.AssessmentResponses.ToListAsync();
        }

        public async Task<AssessmentResponse> GetByIdAsync(int id)
        {
            return await _context.AssessmentResponses.FindAsync(id);
        }

        public async Task<AssessmentResponse> AddAsync(AssessmentResponse response)
        {
            _context.AssessmentResponses.Add(response);
            await _context.SaveChangesAsync();
            return response;
        }

        public async Task<AssessmentResponse> UpdateAsync(int id, AssessmentResponse response)
        {
            var existing = await _context.AssessmentResponses.FindAsync(id);
            if (existing == null) return null;
            existing.Score = response.Score;
            existing.Justification = response.Justification;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var resp = await _context.AssessmentResponses.FindAsync(id);
            if (resp == null) return false;
            _context.AssessmentResponses.Remove(resp);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ResultResponseDto<string>> SaveAssessment(AddAssessmentDto request)
        {
            try
            {
                var assessment = await _context.Assessments
                    .Include(x => x.Responses)
                    .FirstOrDefaultAsync(x =>
                        x.IsActive &&
                        (x.AssessmentID == request.AssessmentID || x.UserCityMappingID == request.UserCityMappingID) );

                if (assessment == null)
                {
                    var ucm = await _context.UserCityMappings
                        .FirstOrDefaultAsync(x=> x.UserCityMappingID == request.UserCityMappingID);
                    if (ucm == null)
                    {
                        return ResultResponseDto<string>.Failure(new[] { "City is not assigned" });
                    }
                    assessment = new Assessment
                    {
                        UserCityMappingID = ucm.UserCityMappingID,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        UserCityMapping = ucm,
                        Responses = new List<AssessmentResponse>()
                    };
                    _context.Assessments.Add(assessment);
                }

                // Add responses
                foreach (var response in request.Responses)
                {
                    var r = new AssessmentResponse
                    {
                        QuestionID = response.QuestionID,
                        QuestionOptionID = response.QuestionOptionID,
                        Justification = response.Justification,
                        Score = response.Score,
                        Assessment = assessment,
                        PillarID = response.PillarID,
                    };
                    assessment.Responses.Add(r);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success(
                    "",
                    new[] { "Assessment saved successfully" }
                );
            }
            catch (Exception ex)
            {
                return ResultResponseDto<string>.Failure(new[] { "failed to saved assessment" });
            }
        }
        public async Task<PaginationResponse<GetAssessmentResponseDto>> GetAssessmentResult(GetAssessmentRequestDto request)
        {
            var user = _context.Users.FirstOrDefault(x=>x.UserID == request.UserId);
            if (user == null) return null;

            var userCityMappingIDs = new List<int>();

            //analyst can search by city and evaluator, admin can search by role and city
            if (user.Role != UserRole.Admin)
            {
                userCityMappingIDs = _context.UserCityMappings
                    .Where(x => !x.IsDeleted 
                    && request.SubUserID.HasValue ? x.UserID == request.SubUserID : x.AssignedByUserId == user.UserID // analyst case
                    || (user.Role == UserRole.Evaluator && x.UserID == request.UserId) // for evaluator 
                    )
                    .Select(x => x.UserCityMappingID).ToList();
            }

            var query =
                from a in _context.Assessments
                    .Include(q => q.Responses)
                where a.IsActive
                      && (!request.CityID.HasValue || a.UserCityMapping.CityID == request.CityID.Value)
                       && (user.Role == UserRole.Admin || userCityMappingIDs.Contains(a.UserCityMappingID))
                join c in _context.Cities
                    .Where(x => !x.IsDeleted
                             && (!request.CityID.HasValue || x.CityID == request.CityID.Value))
                    on a.UserCityMapping.CityID equals c.CityID
                join u in _context.Users
                    .Where(x => !x.IsDeleted
                             && (!request.Role.HasValue || x.Role == request.Role.Value))
                    on a.UserCityMapping.UserID equals u.UserID
                select new GetAssessmentResponseDto
                {
                    AssessmentID = a.AssessmentID,
                    CreatedAt = a.CreatedAt,
                    CityID = c.CityID,
                    CityName = c.CityName,
                    State = c.State,
                    UserID = u.UserID,
                    UserName = u.FullName,
                    Score = a.Responses
                             .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                             .Sum(r => (int?)r.Score ?? 0)
                };

            var response = await query.ApplyPaginationAsync(request);

            var totalScore = _context.Pillars
                .Include(p => p.Questions)
                .SelectMany(p => p.Questions).Count();

            foreach (var item in response.Data)
            {
                item.Score = Math.Round((item.Score / (totalScore * 4)) * 100);
            }

            return response;
        }

        public async Task<PaginationResponse<GetAssessmentQuestionResponseDto>> GetAssessmentQuestion(GetAssessmentQuestoinRequestDto request)
        {
            var user = _context.Users.FirstOrDefault(x => x.UserID == request.UserId);
            if (user == null) return null;

            var userIDs = new List<int>();
            var query = _context.Assessments
                .Include(a => a.Responses)
                    .ThenInclude(r => r.Question)
                        .ThenInclude(q => q.QuestionOptions)
                .Where(a => a.AssessmentID == request.AssessmentID)
                .SelectMany(a => a.Responses)
                .Where(x=> !request.PillarID.HasValue || x.PillarID == request.PillarID.Value)
                .Select(r => new GetAssessmentQuestionResponseDto
                {
                    AssessmentID = request.AssessmentID,
                    PillerID = r.PillarID,
                    PillarName = r.Question.Pillar.PillarName,
                    QuestoinID = r.QuestionID,
                    Score = r.Score,
                    UserID = user.UserID,
                    Justification = r.Justification,
                    QuestionOptionText = r.Question.QuestionOptions
                        .Where(o => o.OptionID == r.QuestionOptionID)
                        .Select(o => o.OptionText)
                        .FirstOrDefault() ?? string.Empty,
                    QuestionText = r.Question.QuestionText
                });

            var response = await query.ApplyPaginationAsync(request);

            return response;
        }

    }
} 