using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public ApiResponse(bool success, string? message, T? data)
        {
            Success = success;
            Message = message;
            Data = data;
        }

        public static ApiResponse<T> Ok(T data, string? message = null)
        {
            return new ApiResponse<T>(true, message, data);
        }

        public static ApiResponse<T> Fail(string message)
        {
            return new ApiResponse<T>(false, message, default);
        }

        /// <summary>
        /// Builds a 404 Not Found response.
        /// </summary>
        /// <param name="message">Explanatory message.</param>
        /// <returns>An unsuccessful API response.</returns>
        public static ApiResponse<T> NotFound(string message = "آیتم مورد نظر یافت نشد")
        {
            return new ApiResponse<T>(false, message, default);
        }

        /// <summary>
        /// Builds a 500 Internal Server Error response.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <returns>An unsuccessful API response.</returns>
        public static ApiResponse<T> Error(string message = "خطای داخلی سرور")
        {
            return new ApiResponse<T>(false, message, default);
        }
    }

}
