namespace DungeonMasterXIV.Rolls;

/// <summary>Why a dice expression was refused, if at all: bad syntax, a limit exceeded, or an arithmetic failure.</summary>
public enum RollFault
{
    None = 0,

    Empty,

    UnknownCharacter,

    Malformed,

    UnbalancedParentheses,

    ModifierWithoutDice,

    NotANumber,

    DivisionByZero,

    TooLong,

    TooManyDice,

    DieTooLarge,

    TooDeeplyNested,

    TooMuchWork,

    ResultOutOfRange,
}
