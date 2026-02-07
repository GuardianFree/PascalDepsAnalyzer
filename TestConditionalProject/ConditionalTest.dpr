program ConditionalTest;

{$APPTYPE CONSOLE}

{$I defines.inc}

uses
  System.SysUtils,
  {$IFDEF DEBUG}
  DebugUnit,
  {$ENDIF}
  {$IFDEF RELEASE}
  ReleaseUnit,
  {$ENDIF}
  {$IF DEFINED(DEBUG) AND DEFINED(LOGGING)}
  VerboseLogUnit,
  {$ENDIF}
  {$IF DEFINED(RELEASE) OR DEFINED(BETA)}
  ProductionUnit,
  {$ENDIF}
  {$IF DEFINED(WIN32) AND DEFINED(DEBUG)}
  Win32DebugUnit,
  {$ENDIF}
  {$IF NOT DEFINED(LOGGING)}
  SilentUnit,
  {$ENDIF}
  {$IF DEFINED(DEBUG)}
  ElseIfFirstUnit,
  {$ELSEIF DEFINED(RELEASE)}
  ElseIfSecondUnit,
  {$ELSE}
  ElseIfDefaultUnit,
  {$ENDIF}
  InlineUnitA, {$IFDEF DEBUG} InlineDebugUnit, {$ENDIF} InlineUnitB,
  {$IFDEF CUSTOM_DEBUG_FLAG}
  CustomFlagUnit,
  {$ENDIF}
  CommonUnit;

begin
  WriteLn('Conditional compilation test');
  ReadLn;
end.
