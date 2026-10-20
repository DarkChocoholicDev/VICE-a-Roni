using MonitorControl;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MonitorControl
{
    /// <summary>
    /// A class for returning a success/failure indicator along with associated messages.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Gets or sets a value indicating whether the request/operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a collection of messages (if any) associated with the result (usually failure-related).
        /// </summary>
        public List<string> Messages { get; set; } = [];

        /// <summary>
        /// Gets the first message (if any) associated with the result.
        /// </summary>
        public string? Message => Messages.FirstOrDefault();

        /// <summary>
        /// Gets or sets the result (if any) that led to this result.
        /// This is usually a lower-level failure that led to the current failure.
        /// </summary>
        public Result? InnerResult { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Result()
        {
            Success = true;
        }

        /// <summary>
        /// Marks the result as failed and sets the associated failure message.
        /// </summary>
        /// <param name="failureMessage">Failure message to associated.</param>
        /// <returns>The current result instance.</returns>
        public Result SetFailureResult(string failureMessage)
        {
            Success = false;
            Messages = [failureMessage];
            return this;
        }

        /// <summary>
        /// Marks the result as failed and assigns the provided failure messages.
        /// </summary>
        /// <param name="failureMessages">A collection of failure messages to associate with the result.</param>
        /// <returns>The current Result instance.</returns>
        public Result SetFailureResult(IEnumerable<string> failureMessages)
        {
            Success = false;
            Messages = [.. failureMessages];
            return this;
        }

        /// <summary>
        /// Copies the Success and Messages properties from the specified result into the current instance.
        /// </summary>
        /// <param name="result">The result whose Success and Messages values are copied.</param>
        /// <returns>The current Result instance with updated Success and Messages values.</returns>
        /// <remarks>Performs a shallow copy of the Messages reference; no cloning of message contents is
        /// performed.</remarks>
        public Result CopyStatus(Result result)
        {
            Success = result.Success;
            Messages = result.Messages;
            return this;
        }
    }

    /// <summary>
    /// A class for returning a success/failure indicator along with associated data and messages.
    /// </summary>
    public class Result<T> : Result
    {
        /// <summary>
        /// Gets or sets the data associated with the result.
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Marks the result as failed and assigns the provided failure messages.
        /// </summary>
        /// <param name="failureMessages">A collection of failure messages to associate with the result.</param>
        /// <returns>The current Result instance.</returns>
        public new Result<T> SetFailureResult(string failureMessage)
        {
            base.SetFailureResult(failureMessage);
            return this;
        }

        /// <summary>
        /// Marks the result as failed and assigns the provided failure messages.
        /// </summary>
        /// <param name="failureMessages">A collection of failure messages to associate with the result.</param>
        /// <returns>The current Result instance.</returns>
        public new Result<T> SetFailureResult(IEnumerable<string> failureMessages)
        {
            base.SetFailureResult((IEnumerable<string>)failureMessages);
            return this;
        }

        /// <summary>
        /// Copies the Success and Messages properties from the specified result into the current instance.
        /// </summary>
        /// <param name="result">The result whose Success and Messages values are copied.</param>
        /// <returns>The current Result instance with updated Success and Messages values.</returns>
        /// <remarks>Performs a shallow copy of the Messages reference; no cloning of message contents is
        /// performed.</remarks>
        public new Result<T> CopyStatus(Result result)
        {
            base.CopyStatus(result);
            return this;
        }

        /// <summary>
        /// Gets the return data if the result is success; otherwise, gets the process result itself.
        /// </summary>
        public object? DataOrFailureResult
        {
            get
            {
                if (base.Success)
                {
                    return Data;
                }
                else
                {
                    return this;
                }
            }
        }
    }
}