// Diagnostic runner for Nashi's isolated Windows CI fixture. Prints call stacks,
// never process memory or local variables; preserves the debuggee's exit code.
#include <windows.h>
#include <dbghelp.h>
#include <cstdio>
#include <string>
#include <vector>
#pragma comment(lib, "dbghelp.lib")

static void stack(HANDLE process, DWORD threadId, DWORD code, DWORD firstChance) {
    std::fprintf(stderr,"\nNATIVE EXCEPTION code=0x%08lx firstChance=%lu thread=%lu\n",code,firstChance,threadId);
    HANDLE thread=OpenThread(THREAD_GET_CONTEXT|THREAD_QUERY_INFORMATION,FALSE,threadId);
    if(!thread)return;
    CONTEXT context{};context.ContextFlags=CONTEXT_FULL;
    if(GetThreadContext(thread,&context)) {
        SymSetOptions(SYMOPT_UNDNAME|SYMOPT_LOAD_LINES|SYMOPT_DEFERRED_LOADS|SYMOPT_FAIL_CRITICAL_ERRORS);
        bool initialized=SymInitialize(process,"srv*build\\native-symbols*https://msdl.microsoft.com/download/symbols",TRUE)!=FALSE;
        if(initialized)SymRefreshModuleList(process);
        STACKFRAME64 frame{};frame.AddrPC.Offset=context.Rip;frame.AddrStack.Offset=context.Rsp;frame.AddrFrame.Offset=context.Rbp;
        frame.AddrPC.Mode=frame.AddrStack.Mode=frame.AddrFrame.Mode=AddrModeFlat;
        for(int n=0;n<50&&frame.AddrPC.Offset;n++) {
            alignas(SYMBOL_INFO) char buffer[sizeof(SYMBOL_INFO)+MAX_SYM_NAME]{};
            auto symbol=reinterpret_cast<SYMBOL_INFO*>(buffer);symbol->SizeOfStruct=sizeof(SYMBOL_INFO);symbol->MaxNameLen=MAX_SYM_NAME;
            DWORD64 displacement=0;IMAGEHLP_MODULE64 module{};module.SizeOfStruct=sizeof(module);
            SymGetModuleInfo64(process,frame.AddrPC.Offset,&module);
            if(SymFromAddr(process,frame.AddrPC.Offset,&displacement,symbol))
                std::fprintf(stderr,"%02d %s!%s + 0x%llx\n",n,module.ModuleName,symbol->Name,static_cast<unsigned long long>(displacement));
            else std::fprintf(stderr,"%02d %s + 0x%llx [0x%llx]\n",n,module.ModuleName,static_cast<unsigned long long>(frame.AddrPC.Offset-module.BaseOfImage),static_cast<unsigned long long>(frame.AddrPC.Offset));
            if(!StackWalk64(IMAGE_FILE_MACHINE_AMD64,process,thread,&frame,&context,nullptr,SymFunctionTableAccess64,SymGetModuleBase64,nullptr))break;
        }
        if(initialized)SymCleanup(process);
    }
    CloseHandle(thread);std::fflush(stderr);
}
int wmain(int argc,wchar_t** argv) {
    if(argc<2)return 2;
    std::wstring command=L"\""+std::wstring(argv[1])+L"\"";
    for(int n=2;n<argc;n++)command+=L" \""+std::wstring(argv[n])+L"\"";
    std::vector<wchar_t> mutableCommand(command.begin(),command.end());mutableCommand.push_back(0);
    STARTUPINFOW startup{};startup.cb=sizeof(startup);PROCESS_INFORMATION process{};
    if(!CreateProcessW(argv[1],mutableCommand.data(),nullptr,nullptr,FALSE,DEBUG_ONLY_THIS_PROCESS,nullptr,nullptr,&startup,&process)) {
        std::fprintf(stderr,"CreateProcess failed: %lu\n",GetLastError());return 3;
    }
    CloseHandle(process.hThread);DWORD result=1;DEBUG_EVENT event{};
    while(WaitForDebugEvent(&event,INFINITE)) {
        DWORD status=DBG_CONTINUE;
        if(event.dwDebugEventCode==EXCEPTION_DEBUG_EVENT) {
            DWORD code=event.u.Exception.ExceptionRecord.ExceptionCode;
            if(code==0xC000000D||!event.u.Exception.dwFirstChance)
                stack(process.hProcess,event.dwThreadId,code,event.u.Exception.dwFirstChance);
            if(code!=EXCEPTION_BREAKPOINT&&code!=EXCEPTION_SINGLE_STEP)status=DBG_EXCEPTION_NOT_HANDLED;
        } else if(event.dwDebugEventCode==CREATE_THREAD_DEBUG_EVENT&&event.u.CreateThread.hThread)CloseHandle(event.u.CreateThread.hThread);
        else if(event.dwDebugEventCode==CREATE_PROCESS_DEBUG_EVENT&&event.u.CreateProcessInfo.hFile)CloseHandle(event.u.CreateProcessInfo.hFile);
        else if(event.dwDebugEventCode==LOAD_DLL_DEBUG_EVENT&&event.u.LoadDll.hFile)CloseHandle(event.u.LoadDll.hFile);
        else if(event.dwDebugEventCode==EXIT_PROCESS_DEBUG_EVENT) {
            result=event.u.ExitProcess.dwExitCode;ContinueDebugEvent(event.dwProcessId,event.dwThreadId,status);break;
        }
        if(!ContinueDebugEvent(event.dwProcessId,event.dwThreadId,status))break;
    }
    CloseHandle(process.hProcess);std::fprintf(stderr,"DEBUGGEE EXIT: 0x%08lx\n",result);return static_cast<int>(result);
}
