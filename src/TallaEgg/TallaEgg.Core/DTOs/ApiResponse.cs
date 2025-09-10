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
        /// ایجاد پاسخ برای موارد پیدا نشدن (404 Not Found)
        /// </summary>
        /// <param name="message">پیام توضیحی</param>
        /// <returns>پاسخ API با وضعیت عدم موفقیت</returns>
        public static ApiResponse<T> NotFound(string message = "آیتم مورد نظر یافت نشد")
        {
            return new ApiResponse<T>(false, message, default);
        }

        /// <summary>
        /// ایجاد پاسخ برای خطاهای سرور (500 Internal Server Error)
        /// </summary>
        /// <param name="message">پیام خطا</param>
        /// <returns>پاسخ API با وضعیت عدم موفقیت</returns>
        public static ApiResponse<T> Error(string message = "خطای داخلی سرور")
        {
            return new ApiResponse<T>(false, message, default);
        }
    }

}
