// MIT License
// 
// Copyright (c) 2022 Felix Kaiser
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;

namespace Foxscore.EasyLogin
{
    /// <summary>
    /// Represents a result of an operation.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam> 
    public class Result<T> 
    {// Result(T value, bool success, Exception error)
        /// <summary>
        /// Initializes a new instance of the <see cref="Result{T}"/> class.
        /// </summary>
        /// <param name="value">The value of the result.</param>
        /// <param name="success">If the operation was successful.</param>
        /// <param name="error">The exception that occurred.</param>
        private Result(T value, bool success, Exception error)
        {
            Value = value;
            IsSuccess = success;
            Error = error;
        }

        /// <summary>
        /// Gets the value of the result.
        /// </summary>
        public readonly T Value;
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public readonly bool IsSuccess;
        /// <summary>
        /// Gets the error that occurred during the operation.
        /// </summary>
        public readonly Exception Error;
        
        /// <summary>
        /// Gets a value indicating whether the operation failed.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="data">The value of the result.</param>
        /// <returns>A successful result.</returns>
        public static Result<T> Success(T data) => new Result<T>(data, true, null);
        
        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="error">The error that occurred during the operation.</param>
        /// <returns>A failed result.</returns>
        public static Result<T> Failure(Exception error) => new Result<T>(default, false, error);
        
        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="message">The error message that occurred during the operation.</param>
        /// <returns>A failed result.</returns>
        public static Result<T> Failure(string message) => new Result<T>(default, false, new Exception(message));
    }
}