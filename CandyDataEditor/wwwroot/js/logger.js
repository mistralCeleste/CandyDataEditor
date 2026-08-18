const LogLevel =
{
    ERROR: 0,
    WARN: 1,
    MESSAGE: 2,
    TRACE: 3,
    DEBUG: 4
};

class Logger
{
    constructor(level = LogLevel.DEBUG)
    {
        this.currentLevel = level;
    }

    setLevel(level)
    {
        this.currentLevel = level;
    }

    error(message, ...args)
    {
        if (this.currentLevel >= LogLevel.ERROR)
        {
            console.error(`[ERROR] ${message}`, ...args);
        }
    }

    warn(message, ...args)
    {
        if (this.currentLevel >= LogLevel.WARN)
        {
            console.warn(`[WARN] ${message}`, ...args);
        }
    }

    message(message, ...args)
    {
        if (this.currentLevel >= LogLevel.MESSAGE)
        {
            console.info(`[INFO] ${message}`, ...args);
        }
    }

    trace(message, ...args)
    {
        if (this.currentLevel >= LogLevel.TRACE)
        {
            console.trace(`[TRACE] ${message}`, ...args);
        }
    }

    debug(message, ...args)
    {
        if (this.currentLevel >= LogLevel.DEBUG)
        {
            console.log(`[DEBUG] ${message}`, ...args);
        }
    }
}
