#pragma once

#ifdef JINPUTWINDOWS_EXPORTS
#define JINPUT_EXPORT __declspec(dllexport)
#else
#define JINPUT_EXPORT __declspec(dllimport)
#endif