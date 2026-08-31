// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export class ServiceNotConfiguredError extends Error {
  service: string;

  constructor(service: string, message: string) {
    super(message);
    this.name = 'ServiceNotConfiguredError';
    this.service = service;
  }
}
