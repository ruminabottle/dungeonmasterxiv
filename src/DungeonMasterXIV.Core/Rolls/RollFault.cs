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

    /// <summary>
    /// A parenthesis was opened and never closed — <c>(1d6</c>. <b>A stray CLOSING parenthesis is
    /// reported as <see cref="Malformed"/>, not as this</b>, because at that point the parser is not
    /// inside a parenthesis and the character is simply unexpected, exactly like <c>&amp;</c>.
    /// </summary>
    /// <remarks>
    /// The second half of that sentence used to be missing and the summary claimed both directions
    /// (BUG-145). Nothing produced the closed-and-never-opened case, so a caller writing a handler
    /// from these summaries would put a stray <c>)</c> under this branch and never reach it — and
    /// would not find out, because the code compiles and the branch is simply never taken.
    /// </remarks>
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

    /// <summary>
    /// A total, or a step on the way to one, did not fit in a 32-bit integer — <c>1073741824*2</c>.
    /// </summary>
    /// <remarks>
    /// <b>A REFUSAL BECAUSE THE ALTERNATIVES ARE BOTH FORBIDDEN (BUG-143).</b> Unchecked, this
    /// arithmetic either wraps — answering <c>2000000000+2000000000</c> as <c>-294967296</c>,
    /// confidently and wrongly, which R-2.1 rules out in the words <i>"it never silently rolls
    /// something else"</i> — or, for <c>int.MinValue / -1</c>, the one case the hardware cannot
    /// wrap, throws out of a method whose own summary promises it <i>"never throws for bad input"</i>.
    /// </remarks>
    ResultOutOfRange,
}
