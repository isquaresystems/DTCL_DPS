#pragma once

#include <cstdio>
#include <cstring>

class SimpleLogger
{
public:
    enum Level {
        LOG_DEBUG,
        LOG_INFO,
        LOG_ERROR,
        LOG_CRITICAL
    };

    static SimpleLogger& getInstance()
    {
        static SimpleLogger instance;
        return instance;
    }

    void log(Level level, const char* text)
    {
        /*if (currentPos >= MaxSize - 1) return;

        const char* levelStr = getLevelString(level);
        std::sprintf(buffer + currentPos, "%s %s\n", levelStr, text);
        currentPos += std::strlen(levelStr) + std::strlen(text) + 2;

        if (currentPos >= MaxSize)
            currentPos = MaxSize - 1;*/
    }

    void print() const
    {
        std::printf("%s", buffer);
    }

    void clear()
    {
        currentPos = 0;
        buffer[0] = '\0';
    }

private:
    SimpleLogger() : currentPos(0)
    {
        //std::memset(buffer, 0, sizeof(buffer));
    }

    SimpleLogger(const SimpleLogger&);
    SimpleLogger& operator=(const SimpleLogger&);

    const char* getLevelString(Level level) const
    {
        switch (level)
        {
        case LOG_DEBUG: return "[DEBUG]";
        case LOG_INFO: return "[INFO]";
        case LOG_ERROR: return "[ERROR]";
        case LOG_CRITICAL: return "[CRITICAL]";
        default: return "[UNKNOWN]";
        }
    }

private:
    enum { MaxSize = 10240 };
    char buffer[MaxSize];
    size_t currentPos;
};
