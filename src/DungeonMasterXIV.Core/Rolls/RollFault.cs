namespace DungeonMasterXIV.Rolls;

/// <summary>
/// What was wrong with an expression that was refused. <b>Every refusal names its fault</b> — R-2.1
/// forbids silently rolling something else, and R-2.1a requires a bound breach to say which bound.
/// </summary>
/// <remarks>
/// <para>
/// <b>This enum is why a refusal is checkable.</b> A-2.3 and A-2.3a both require the refusal to name
/// the fault, and a test that only asserts "some message came back" would pass an evaluator that
/// answered every bad expression with the same shrug. The message text is for a human; this value is
/// what a test pins.
/// </para>
/// <para>
/// <b>NOTHING HERE MEANS "THE ROLL FAILED".</b> Every member is a fault in the EXPRESSION — the
/// product deciding whether a roll succeeded is deferred by the human (R-2.1, D-4), and the
/// evaluator is precisely where that would first become expressible. A member such as
/// <c>RollFailed</c> would be that decision arriving through the back door.
/// </para>
/// </remarks>
public enum RollFault
{
    /// <summary>No fault. The expression evaluated.</summary>
    None = 0,

    /// <summary>The expression was empty or only whitespace.</summary>
    Empty,

    /// <summary>A character appeared that the grammar has no meaning for.</summary>
    UnknownCharacter,

    /// <summary>
    /// The tokens do not form an expression — a trailing <c>+</c>, a missing operand, an operator
    /// where a number belongs.
    /// </summary>
    Malformed,

    /// <summary>A parenthesis was opened and not closed, or closed and never opened.</summary>
    UnbalancedParentheses,

    /// <summary>A modifier was attached to something that is not a dice term, such as <c>5kh2</c>.</summary>
    ModifierWithoutDice,

    /// <summary>A number was written that does not fit, or a die with fewer than one face.</summary>
    NotANumber,

    /// <summary>Division by zero. Refused rather than yielding an infinity a total cannot hold.</summary>
    DivisionByZero,

    /// <summary>The expression text exceeded <see cref="RollLimits.MaxLength"/>.</summary>
    TooLong,

    /// <summary>A term asked for more dice than <see cref="RollLimits.MaxDicePerTerm"/> allows.</summary>
    TooManyDice,

    /// <summary>A die had more faces than <see cref="RollLimits.MaxDieSize"/> allows.</summary>
    DieTooLarge,

    /// <summary>Nesting exceeded <see cref="RollLimits.MaxNestingDepth"/>.</summary>
    TooDeeplyNested,

    /// <summary>
    /// Evaluating would roll more dice than <see cref="RollLimits.MaxWork"/> allows. <b>The bound
    /// that catches an expression whose SHAPE is legal and whose COST is not</b> — exploding dice on
    /// a large pool, for instance.
    /// </summary>
    TooMuchWork,
}
