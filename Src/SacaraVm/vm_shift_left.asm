header_VmShiftLeft
vm_shift_left PROC
	push ebp
	mov ebp, esp	
	
	; read the first operand
	push [ebp+arg0]
	call_vm_stack_pop_enc
	push eax

	; read the second operand
	push [ebp+arg0]
	call_vm_stack_pop_enc
	mov cl, al

	; do operation
	pop eax
	shr eax, cl

	; push result
	push eax
	push [ebp+arg0]
	call_vm_stack_push_enc

	mov esp, ebp
	pop ebp
	ret
vm_shift_left ENDP
header_marker