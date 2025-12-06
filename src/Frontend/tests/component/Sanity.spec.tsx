import React from 'react';
import { test, expect } from '@playwright/experimental-ct-react';

test('sanity mount inline jsx', async ({ mount }) => {
  const component = await mount(<div>hello</div>);
  await expect(component.locator('text=hello')).toBeVisible();
});