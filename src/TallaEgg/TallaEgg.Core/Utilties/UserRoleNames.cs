using TallaEgg.Core.Enums.User;

namespace TallaEgg.Core.Utilties;

/// <summary>
/// Turns what an operator types into a <see cref="UserRole"/>, and a role back into the words
/// they will recognise.
///
/// <para>
/// Kept out of the bot handler so the mapping can be tested on its own. The handler's job is
/// to route a message; deciding that "مدير" (Arabic yeh) means <see cref="UserRole.Admin"/> is
/// a separate question with its own edge cases, and it is the part that will actually go wrong.
/// </para>
///
/// <para>
/// <b>Numbers are accepted as well as names.</b> The enum values are stored in the database and
/// appear in logs and diagnostics, so an operator reading <c>role=2</c> there must be able to
/// type <c>2</c> back. Requiring the Persian word would mean the values they can see are values
/// they cannot use.
/// </para>
/// </summary>
public static class UserRoleNames
{
    /// <summary>
    /// The roles an operator may assign, in ascending order of privilege.
    ///
    /// <see cref="UserRole.User"/> is deliberately absent. It is a second ordinary-user value
    /// that duplicates <see cref="UserRole.RegularUser"/> — the enum itself carries a comment
    /// asking what the difference is — so offering both would only invite picking the wrong
    /// one. It is still recognised on input and displayed correctly; it just cannot be chosen
    /// as a target when a clear alternative exists.
    /// </summary>
    public static readonly UserRole[] Assignable =
    [
        UserRole.RegularUser,
        UserRole.Accountant,
        UserRole.Admin,
        UserRole.SuperAdmin
    ];

    /// <summary>What each role is called when the bot reports it back.</summary>
    public static string Display(UserRole role) => role switch
    {
        UserRole.RegularUser => "کاربر عادی",
        UserRole.User => "کاربر عادی",
        UserRole.Accountant => "حسابدار",
        UserRole.Admin => "مدیر",
        UserRole.SuperAdmin => "مدیر ارشد",
        _ => role.ToString()
    };

    /// <summary>A one-line list of the accepted inputs, for the help and the error message.</summary>
    public static string AssignableList() =>
        string.Join("، ", Assignable.Select(r => $"{Display(r)} ({(int)r})"));

    /// <summary>
    /// Parses a role from a name (Persian or English) or from its numeric value.
    ///
    /// Returns false rather than guessing. A role change is not a place to be helpful with a
    /// near match: silently reading "مدیر ارشد" as "مدیر" would hand out the wrong authority
    /// and the operator would see a success message either way.
    /// </summary>
    public static bool TryParse(string? input, out UserRole role)
    {
        role = default;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Arabic yeh/kaf reach us from some keyboards and are not equal to the Persian letters
        // by ordinal comparison, so "مدير" would otherwise fail against "مدیر". Zero-width
        // non-joiner is removed for the same reason: "مدیر‌ارشد" is typed both ways.
        var normalized = input.Trim()
            .Replace('ي', 'ی')    // ARABIC YEH  -> FARSI YEH
            .Replace('ك', 'ک')    // ARABIC KAF  -> KEHEH
            .Replace("‌", " ")         // ZWNJ        -> space
            .Replace("‏", "")          // RLM
            .Replace("‎", "");         // LRM

        // Collapse repeated whitespace so "مدیر   ارشد" matches "مدیر ارشد", and lower-case so
        // the English aliases below can be written once. Persian letters are unaffected.
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

        // Persian digits may arrive unconverted when the caller has not normalised the text.
        var asEnglishDigits = Utils.ConvertPersianDigitsToEnglish(normalized);

        if (int.TryParse(asEnglishDigits, out var numeric) && Enum.IsDefined(typeof(UserRole), numeric))
        {
            role = (UserRole)numeric;
            return true;
        }

        switch (normalized)
        {
            case "کاربر عادی":
            case "کاربر":
            case "عادی":
            case "regularuser":
            case "user":
                role = UserRole.RegularUser;
                return true;

            case "حسابدار":
            case "accountant":
                role = UserRole.Accountant;
                return true;

            case "مدیر":
            case "ادمین":
            case "admin":
                role = UserRole.Admin;
                return true;

            case "مدیر ارشد":
            case "مدیرارشد":
            case "سوپر ادمین":
            case "superadmin":
                role = UserRole.SuperAdmin;
                return true;

            default:
                return false;
        }
    }
}
