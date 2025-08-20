using System;

namespace AssessmentPlatform.Models
{
    public enum ScoreValue { Four = 4, Three = 3, Two = 2, One = 1, Zero = 0, NA, Unknown }
    public class AssessmentResponse
    {
        public int ResponseID { get; set; }
        public int AssessmentID { get; set; }
        public int QuestionID { get; set; }
        public int QuestionOptionID { get; set; }
        public int PillarID { get; set; }
        public ScoreValue? Score { get; set; }
        public string Justification { get; set; } 
        public Assessment Assessment { get; set; } 
        public Question Question { get; set; } 
    }
} 