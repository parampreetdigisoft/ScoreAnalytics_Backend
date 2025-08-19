using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.QuestionDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
namespace AssessmentPlatform.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly ApplicationDbContext _context;
        public QuestionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Pillar>> GetPillarsAsync()
        {
            return await _context.Pillars.OrderBy(p => p.DisplayOrder).ToListAsync();
        }

        public async Task<PaginationResponse<GetQuestionRespones>> GetQuestionsAsync(GetQuestionRequestDto request)
        {
            var query =
                from q in _context.Questions
                    .Include(q => q.Pillar)
                    .Include(o => o.QuestionOptions)
                where !q.IsDeleted
                   && (!request.PillarID.HasValue || q.PillarID == request.PillarID.Value)
                select new GetQuestionRespones
                {
                    QuestionID = q.QuestionID,
                    QuestionText = q.QuestionText,
                    PillarID = q.PillarID,
                    PillarName = q.Pillar.PillarName,
                    DisplayOrder = q.DisplayOrder,
                    QuestionOptions = q.QuestionOptions.ToList()
                };

            var response = await query.ApplyPaginationAsync(request);

            return response;
        }

        public async Task<Question> AddQuestionAsync(Question q)
        {
            _context.Questions.Add(q);
            await _context.SaveChangesAsync();
            return q;
        }

        public async Task<Question> EditQuestionAsync(int id, Question q)
        {
            var existing = await _context.Questions.FindAsync(id);
            if (existing == null) return null;
            existing.QuestionText = q.QuestionText;
            existing.PillarID = q.PillarID;
            existing.DisplayOrder = q.DisplayOrder;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteQuestionAsync(int id)
        {
            var q = await _context.Questions.FindAsync(id);
            if (q == null) return false;

            q.IsDeleted = true;
            _context.Questions.Update(q);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ResultResponseDto<string>> AddUpdateQuestion(AddUpdateQuestionDto q)
        {
            try
            {
                var pillarQuestions = await _context.Questions
                .Include(x => x.QuestionOptions)
                .Where(x => x.PillarID == q.PillarID)
                .ToListAsync();

                var totalQuestions = pillarQuestions.Count;
                var question = pillarQuestions.FirstOrDefault(x => x.QuestionID == q.QuestionID) ?? new Question();

                // Common properties
                question.IsDeleted = false;
                question.QuestionText = q.QuestionText;
                question.PillarID = q.PillarID;

                // Sync options (Add / Update / Delete)
                var incomingOptions = q.QuestionOptions ?? new List<QuestionOption>();


                foreach (var o in incomingOptions)
                {
                    var option = question.QuestionOptions.FirstOrDefault(x => x.OptionID == o.OptionID && x.OptionID > 0);

                    if (option == null) // new option
                    {
                        option = new QuestionOption
                        {
                            OptionText = o.OptionText,
                            DisplayOrder = (o.ScoreValue ?? -1) + 1,
                            ScoreValue = o.ScoreValue,
                            Question = question
                        };
                        question.QuestionOptions.Add(option);
                    }
                    else // update existing
                    {
                        option.OptionText = o.OptionText;
                        option.DisplayOrder = (o.ScoreValue ?? -1) + 1;
                        option.ScoreValue = o.ScoreValue;
                    }
                }
                // Add default N/A and Unknown only for new question
                if (question.QuestionID == 0)
                {
                    question.DisplayOrder = totalQuestions + 1;

                    question.QuestionOptions.Add(new QuestionOption
                    {
                        DisplayOrder = 6,
                        OptionText = "N/A",
                        ScoreValue = null
                    });
                    question.QuestionOptions.Add(new QuestionOption
                    {
                        DisplayOrder = 7,
                        OptionText = "Unknown",
                        ScoreValue = null
                    });
                }

                var optionIdsFromDto = incomingOptions.Select(x => x.OptionID).ToHashSet();
                var optionsToRemove = question.QuestionOptions
                    .Where(x => x.OptionID > 0 && x.ScoreValue != null && !optionIdsFromDto.Contains(x.OptionID))
                    .ToList();

                foreach (var remove in optionsToRemove)
                {
                    _context.QuestionOptions.Remove(remove);
                }

                // Save Question
                if (question.QuestionID > 0)
                    _context.Questions.Update(question);
                else
                    _context.Questions.Add(question);

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { q.QuestionID > 0 ? "Question updated successfully" : "Question saved successfully" });
            }
            catch (Exception ex)
            {
                return ResultResponseDto<string>.Failure(new[] { ex.Message });
            }
        }

        public async Task<ResultResponseDto<List<GetQuestionByCityRespones>>> GetQuestionsByCityIdAsync(CityPillerRequestDto request)
        {
            // Load assessment once (if exists)
            var assessment = await _context.Assessments
                .Where(a => a.UserID == request.UserID && a.CityID == request.CityID)
                .FirstOrDefaultAsync();

            // Get distinct answered pillar IDs
            var answeredPillarIds = await _context.AssessmentResponses
                .Where(r => r.Assessment.UserID == request.UserID && r.Assessment.CityID == request.CityID)
                .Select(r => r.Question.PillarID)
                .Distinct()
                .ToListAsync();

            // Get next unanswered pillar
            var nextPillar = await _context.Pillars
                .Include(p => p.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .Where(p => !answeredPillarIds.Contains(p.PillarID))
                .OrderBy(p => p.DisplayOrder)
                .FirstOrDefaultAsync();

            if (nextPillar == null)
            {
                return ResultResponseDto<List<GetQuestionByCityRespones>>.Failure(new[] { "You have submitted assessment for this city" });
            }

            // Project questions
            var questions = nextPillar.Questions
            .OrderBy(q => q.DisplayOrder)
            .Select(q => new GetQuestionByCityRespones
            {
                QuestionID = q.QuestionID,
                QuestionText = q.QuestionText,
                PillarID = q.PillarID,
                PillarName = nextPillar.PillarName,  // avoid extra nav
                DisplayOrder = q.DisplayOrder,
                QuestionOptions = q.QuestionOptions.ToList(),
                AssessmentID = assessment?.AssessmentID ?? 0,
                PillarDisplayOrder = nextPillar.DisplayOrder
            }).ToList();

            return ResultResponseDto<List<GetQuestionByCityRespones>>.Success(questions, new[] { "get questions successfully" });
        }

    }
}