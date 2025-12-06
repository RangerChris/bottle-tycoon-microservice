import React from 'react';
import { test, expect } from '@playwright/experimental-ct-react';
import HealthCheck from '../../src/components/HealthCheck';

test.use({ viewport: { width: 800, height: 600 } });

test('HealthCheck shows loading then content when fetch succeeds', async ({ mount }) => {
  // mount the component
  const component = await mount(<HealthCheck />);
  // initially shows loading
  await expect(component.locator('text=Loading...')).toBeVisible();
  // wait for effect to complete (component fetches /health). We stub network via window.fetch
  await component.evaluate(() => {
    // override fetch to return OK immediately
    // @ts-ignore
    window.fetch = async () => ({ text: async () => 'OK' });
  });
  // wait a moment for component to update
  await component.waitForSelector('text=OK', { timeout: 3000 });
  await expect(component.locator('text=OK')).toBeVisible();
});