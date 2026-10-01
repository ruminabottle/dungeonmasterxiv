namespace DungeonMasterXIV.Rolls;

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
