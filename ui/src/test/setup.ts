import "@testing-library/jest-dom/vitest";
import * as axeMatchers from "vitest-axe/matchers";
import { expect, afterEach } from "vitest";
import { cleanup } from "@testing-library/react";

// jest-dom + axe matchers for every test (KDM-029).
expect.extend(axeMatchers);

afterEach(() => cleanup());
