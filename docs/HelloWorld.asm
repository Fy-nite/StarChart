; HelloWorld.asm
; Minimal example for StarChart AssemblyRuntime + StarChartAssemblyHost
; - Uses HOST syscall path: write (rax=1) to stdout (rdi=1)
; - Exits using syscall (rax=60)


msg:    db "Hello, World!", 10
len:    equ $ - msg


_start:
    ; prepare write(fd=1, buf=msg, count=len)
    mov rax, 1        ; syscall number: write
    mov rdi, 1        ; fd = stdout
    mov rsi, msg      ; pointer to message (avoid bracket syntax)
    mov rdx, len      ; length
    syscall      ; call into host (AssemblyHost.HandleSyscall will read rax/rdi/rsi/rdx)

    ; exit(status=0)
    mov rax, 60       ; syscall number: exit
    mov rdi, 0        ; status 0
    HOST syscall

; End of HelloWorld.asm
