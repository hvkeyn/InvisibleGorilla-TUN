package tun

import (
	"C"
	"fmt"
	"os/exec"
	"strconv"
	"strings"
)

//export SetInterfaceAddress
func SetInterfaceAddress(device *C.char, address *C.char) {
	process := "netsh"
	args := []string{
		"interface", "ip", "set", "address",
		"name=" + C.GoString(device),
		"addr=" + C.GoString(address),
		"source=static",
		"mask=255.255.255.0",
		"gateway=none",
	}

	runProcess(process, args)
}

//export SetInterfaceDns
func SetInterfaceDns(device *C.char, dns *C.char) {
	process := "netsh"
	args := []string{
		"interface", "ip", "set", "dns",
		"name=" + C.GoString(device),
		"static",
		C.GoString(dns),
	}

	runProcess(process, args)
}

//export SetRoutes
func SetRoutes(server *C.char, address *C.char, gateway *C.char, index int) {
	srv := C.GoString(server)
	addr := C.GoString(address)
	gw := C.GoString(gateway)
	ifIndex := strconv.Itoa(index)

	if strings.TrimSpace(gw) == "" {
		fmt.Println("error | SetRoutes: physical gateway is empty")
		return
	}

	// Reach the VPN server directly through the physical gateway so the tunnel's own
	// packets are not routed back into the tunnel (which would kill connectivity).
	runProcess("cmd", []string{
		"/c", "route", "add",
		srv, "mask", "255.255.255.255", gw,
	})

	// Force the TUN interface to the lowest metric so its routes win over the physical
	// adapter's default route.
	runProcess("netsh", []string{
		"interface", "ipv4", "set", "interface", ifIndex, "metric=1",
	})

	// Capture all traffic through the TUN with two /1 routes. A /1 prefix is more
	// specific than the existing 0.0.0.0/0 default route, so it always takes precedence
	// regardless of the physical adapter's metric (the classic "def1" redirect-gateway
	// trick). A single competing 0.0.0.0/0 would otherwise lose on metric and let
	// traffic leak through the physical NIC (real IP stays exposed).
	runProcess("cmd", []string{
		"/c", "route", "add",
		"0.0.0.0", "mask", "128.0.0.0", addr, "metric", "1", "IF", ifIndex,
	})
	runProcess("cmd", []string{
		"/c", "route", "add",
		"128.0.0.0", "mask", "128.0.0.0", addr, "metric", "1", "IF", ifIndex,
	})

	// Keep the literal default route as well (low metric) for completeness.
	runProcess("cmd", []string{
		"/c", "route", "add",
		"0.0.0.0", "mask", "0.0.0.0", addr, "metric", "1", "IF", ifIndex,
	})
}

func runProcess(process string, args []string) {
	cmd := exec.Command(process, args...)
	err := cmd.Run()

	if err != nil {
		fmt.Println("error | failed to start process >", process, err)
		return
	}
}
