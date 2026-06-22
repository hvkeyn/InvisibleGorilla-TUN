package tun

import (
	"C"
	"fmt"
)

func RunTest(device string, proxy string) {
	fmt.Println("start testing...")
	cdevice := C.CString(device)
	cproxy := C.CString(proxy)
	cbind := C.CString("")

	fmt.Println("device:", device)
	fmt.Println("proxy:", proxy)

	StartTunnel(cdevice, cproxy, cbind)
	fmt.Println("end of test.")
}
