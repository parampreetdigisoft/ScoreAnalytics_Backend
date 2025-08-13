namespace AssessmentPlatform.Common.Models
{
    public class ResultResponseDto<T> 
    {
        internal ResultResponseDto(bool succeeded, T result, IEnumerable<string> errors, IEnumerable<string> messages, int? returnId, bool? isExist)
        {
            Succeeded = succeeded;
            Result = result;
            Errors = errors?.ToArray() ?? Array.Empty<string>();
            Messages = messages?.ToArray() ?? Array.Empty<string>();
            ReturnId = returnId;
            IsExist = isExist;
        }

        // Properties
        public bool Succeeded { get; init; }
        public T Result { get; init; }
        public string[] Errors { get; init; }
        public string[] Messages { get; init; }
        public int? ReturnId { get; init; }
        public bool? IsExist { get; init; }

        public static ResultResponseDto<T> Success(T result=default,IEnumerable<string>? messages = null,int? returnId = null)
        {
            return new ResultResponseDto<T>(
                succeeded: true,
                result: result,
                errors: null,
                messages: messages,
                returnId: returnId,
                isExist: null
            );
        }

        public static ResultResponseDto<T> Failure(IEnumerable<string> errors, bool? isExist = null)
        {
            return new ResultResponseDto<T>(
                succeeded: false,
                result: default,
                errors: errors,
                messages: null,
                returnId: null,
                isExist: isExist
            );
        }
    }

}
