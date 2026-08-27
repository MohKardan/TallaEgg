namespace TallaEgg.Core.ErrorHandling;

/// <summary>
/// A rule of the business said no, and <see cref="System.Exception.Message"/> is the sentence to
/// show the customer.
///
/// <para>
/// That contract is the whole point of the type. Every API endpoint used to end in
/// <c>catch (Exception ex) =&gt; Fail(ex.Message)</c>, which returned the message of whatever had
/// been thrown. Sometimes that was Persian text written for the customer — "کیف پول وجود ندارد" —
/// and sometimes it was a developer's <c>ArgumentException("ReferenceId cannot be empty")</c>, or a
/// <c>SqlException</c>, or a <c>NullReferenceException</c>. The endpoint could not tell them apart,
/// because both kinds arrived as <c>ArgumentException</c>, so a Persian-speaking customer could be
/// shown a .NET diagnostic. Throwing this type is the statement that a message is meant for them.
/// </para>
///
/// <para>
/// <b>What does not belong here:</b> a failure the customer did not cause and cannot act on. A
/// database read that threw, an HTTP call that timed out, a bug. Those must reach
/// <see cref="GlobalExceptionHandler"/>, which logs them with a trace id and returns the generic
/// message. Wrapping one of them in Persian only makes an outage look like a rejected request.
/// </para>
///
/// <para>
/// Write the message the way <c>BotMsgs</c> writes messages: Persian, no Latin text, and specific
/// enough that the customer knows what to do next.
/// </para>
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    /// <summary>
    /// For a rule broken because something underneath failed, where the customer still needs a
    /// sentence of their own. The inner exception is for the log; only <paramref name="message"/>
    /// is ever shown.
    /// </summary>
    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
