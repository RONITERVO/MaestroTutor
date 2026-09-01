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

export class ServiceHttpError extends Error {
  status: number;
  code?: string;

  constructor(message: string, status: number, code?: string) {
    super(message);
    this.name = 'ServiceHttpError';
    this.status = status;
    this.code = code;
  }
}
