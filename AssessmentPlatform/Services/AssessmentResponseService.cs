using Microsoft.EntityFrameworkCore;
using AssessmentPlatform.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AssessmentDto;

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
                        x.AssessmentID == request.AssessmentID &&
                        x.CityID == request.CityID);

                if (assessment == null)
                {
                    assessment = new Assessment
                    {
                        CityID = request.CityID,
                        UserID = request.UserID,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
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
                        Assessment = assessment
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
    }
} 