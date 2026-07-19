package main

import "testing"

func TestMessage(t *testing.T) {
	if got := message(); got != "Hello from Go" {
		t.Fatalf("message() = %q", got)
	}
}
